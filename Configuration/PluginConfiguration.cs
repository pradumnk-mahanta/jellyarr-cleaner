using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JellyArrCleaner.Configuration;

public enum MovieDeleteBehavior
{
    DeleteFileOnly,
    DeleteAndRemoveMovie
}

public enum SeriesDeleteBehavior
{
    DeleteFilesOnly,
    DeleteAndRemoveShow
}

public class PluginConfiguration : BasePluginConfiguration
{
    public string RadarrUrl { get; set; } = "http://radarr:7878";
    public string RadarrApiKey { get; set; } = string.Empty;
    public MovieDeleteBehavior MovieBehavior { get; set; } = MovieDeleteBehavior.DeleteFileOnly;

    public string SonarrUrl { get; set; } = "http://sonarr:8989";
    public string SonarrApiKey { get; set; } = string.Empty;
    public SeriesDeleteBehavior SeriesBehavior { get; set; } = SeriesDeleteBehavior.DeleteFilesOnly;
}