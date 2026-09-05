using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.JellyArrCleaner.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyArrCleaner.Services;

public class ArrApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ArrApiService> _logger;

    public ArrApiService(IHttpClientFactory httpClientFactory, ILogger<ArrApiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    #region Radarr Operations

    public async Task HandleMovieDeleteAsync(string tmdbId, string movieName, PluginConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.RadarrApiKey) || string.IsNullOrWhiteSpace(config.RadarrUrl))
        {
            return;
        }

        var client = CreateClient(config.RadarrUrl, config.RadarrApiKey);

        var lookupRes = await client.GetAsync($"/api/v3/movie?tmdbId={tmdbId}");
        if (!lookupRes.IsSuccessStatusCode) return;

        using var jsonDoc = await JsonDocument.ParseAsync(await lookupRes.Content.ReadAsStreamAsync());
        if (jsonDoc.RootElement.GetArrayLength() == 0) return;

        var movieObj = jsonDoc.RootElement[0];
        var radarrId = movieObj.GetProperty("id").GetInt32();

        if (config.MovieBehavior == MovieDeleteBehavior.DeleteAndRemoveMovie)
        {
            var deleteUrl = $"/api/v3/movie/{radarrId}?deleteFiles=true&addImportExclusion=false";
            var deleteRes = await client.DeleteAsync(deleteUrl);
            if (deleteRes.IsSuccessStatusCode)
            {
                _logger.LogInformation("[JellyArrCleaner] Deleted and removed movie '{Name}' (Radarr ID: {Id})", movieName, radarrId);
            }
        }
        else
        {
            if (movieObj.TryGetProperty("movieFileId", out var fileProp) && fileProp.GetInt32() > 0)
            {
                await client.DeleteAsync($"/api/v3/moviefile/{fileProp.GetInt32()}");
            }

            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(movieObj.GetRawText())!;
            dict["monitored"] = false;
            var content = new StringContent(JsonSerializer.Serialize(dict), Encoding.UTF8, "application/json");
            await client.PutAsync($"/api/v3/movie/{radarrId}", content);

            _logger.LogInformation("[JellyArrCleaner] Deleted file and unmonitored movie '{Name}' in Radarr", movieName);
        }
    }

    #endregion

    #region Sonarr Operations

    public async Task HandleSeriesDeleteAsync(string tvdbId, string seriesName, PluginConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.SonarrApiKey) || string.IsNullOrWhiteSpace(config.SonarrUrl))
        {
            return;
        }

        var client = CreateClient(config.SonarrUrl, config.SonarrApiKey);
        var seriesId = await GetSonarrSeriesIdAsync(client, tvdbId);
        if (!seriesId.HasValue) return;

        if (config.SeriesBehavior == SeriesDeleteBehavior.DeleteAndRemoveShow)
        {
            var deleteUrl = $"/api/v3/series/{seriesId.Value}?deleteFiles=true&addImportExclusion=false";
            var deleteRes = await client.DeleteAsync(deleteUrl);
            if (deleteRes.IsSuccessStatusCode)
            {
                _logger.LogInformation("[JellyArrCleaner] Deleted and removed show '{Name}' (Sonarr ID: {Id})", seriesName, seriesId.Value);
            }
        }
        else
        {
            var fileIds = await GetSeriesEpisodeFilesAsync(client, seriesId.Value);
            foreach (var fileId in fileIds)
            {
                await client.DeleteAsync($"/api/v3/episodefile/{fileId}");
            }
            await SetSeriesMonitoringAsync(client, seriesId.Value, false);
            _logger.LogInformation("[JellyArrCleaner] Deleted files and unmonitored show '{Name}' in Sonarr", seriesName);
        }
    }

    #endregion

    #region Internal Helpers

    private HttpClient CreateClient(string baseUrl, string apiKey)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private async Task<int?> GetSonarrSeriesIdAsync(HttpClient client, string tvdbId)
    {
        var res = await client.GetAsync($"/api/v3/series?tvdbId={tvdbId}");
        if (!res.IsSuccessStatusCode) return null;

        using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
        if (doc.RootElement.GetArrayLength() == 0) return null;

        return doc.RootElement[0].GetProperty("id").GetInt32();
    }

    private async Task<List<int>> GetSeriesEpisodeFilesAsync(HttpClient client, int seriesId)
    {
        var res = await client.GetAsync($"/api/v3/episodefile?seriesId={seriesId}");
        var files = new List<int>();
        if (!res.IsSuccessStatusCode) return files;

        using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var idProp))
            {
                files.Add(idProp.GetInt32());
            }
        }
        return files;
    }

    private async Task SetSeriesMonitoringAsync(HttpClient client, int seriesId, bool monitored)
    {
        var res = await client.GetAsync($"/api/v3/series/{seriesId}");
        if (!res.IsSuccessStatusCode) return;

        using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(doc.RootElement.GetRawText())!;
        dict["monitored"] = monitored;

        var content = new StringContent(JsonSerializer.Serialize(dict), Encoding.UTF8, "application/json");
        await client.PutAsync($"/api/v3/series/{seriesId}", content);
    }

    #endregion
}