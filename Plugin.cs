using System;
using System.Collections.Generic;
using System.IO;
using Jellyfin.Plugin.JellyArrCleaner.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.JellyArrCleaner;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasPluginImage
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "JellyArr Cleaner";

    public override Guid Id => Guid.Parse("e2b1096a-79b8-4665-b778-90f7d5402a39");

    public override string Description => "Synchronizes Movie and Series deletions in Jellyfin with Radarr and Sonarr.";

    public ImageFormat ImageFormat => ImageFormat.Svg;

    public Stream GetPluginImage()
    {
        var type = GetType();
        return type.Assembly.GetManifestResourceStream($"{type.Namespace}.logo.svg")
               ?? throw new InvalidOperationException("Embedded logo.svg not found.");
    }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = this.Name,
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.PluginConfigurationPage.html"
            }
        };
    }
}