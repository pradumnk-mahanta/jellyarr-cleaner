using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.JellyArrCleaner.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyArrCleaner.Services;

public class ArrDeleteSyncService : IHostedService, IDisposable
{
    private readonly ILibraryManager _libraryManager;
    private readonly ArrApiService _apiService;
    private readonly ILogger<ArrDeleteSyncService> _logger;

    public ArrDeleteSyncService(
        ILibraryManager libraryManager,
        ArrApiService apiService,
        ILogger<ArrDeleteSyncService> logger)
    {
        _libraryManager = libraryManager;
        _apiService = apiService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemRemoved += OnItemRemoved;
        _logger.LogInformation("[JellyArrCleaner] Service started and monitoring for movie and series deletions.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemRemoved -= OnItemRemoved;
        _logger.LogInformation("[JellyArrCleaner] Service stopped.");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _libraryManager.ItemRemoved -= OnItemRemoved;
    }

    private async void OnItemRemoved(object? sender, ItemChangeEventArgs e)
    {
        try
        {
            var config = Plugin.Instance?.Configuration;
            if (config == null) return;

            if (e.Item is Movie movie)
            {
                var tmdbId = movie.GetProviderId(MetadataProvider.Tmdb) 
                             ?? (movie.ProviderIds.TryGetValue("Tmdb", out var id) ? id : null);

                if (!string.IsNullOrEmpty(tmdbId))
                {
                    await _apiService.HandleMovieDeleteAsync(tmdbId, movie.Name, config);
                }
                else
                {
                    _logger.LogWarning("[JellyArrCleaner] Movie '{Name}' had no TMDb ID; skipped Radarr sync.", movie.Name);
                }
            }
            else if (e.Item is Series series)
            {
                var tvdbId = series.GetProviderId(MetadataProvider.Tvdb) 
                             ?? (series.ProviderIds.TryGetValue("Tvdb", out var id) ? id : null);

                if (!string.IsNullOrEmpty(tvdbId))
                {
                    await _apiService.HandleSeriesDeleteAsync(tvdbId, series.Name, config);
                }
                else
                {
                    _logger.LogWarning("[JellyArrCleaner] Series '{Name}' had no TVDb ID; skipped Sonarr sync.", series.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JellyArrCleaner] Error processing deletion for: {Name}", e.Item?.Name);
        }
    }
}