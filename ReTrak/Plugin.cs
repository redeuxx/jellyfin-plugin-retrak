using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using ReTrak.Configuration;

namespace ReTrak;

/// <summary>
/// Plugin class for ReTrak syncing.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "ReTrak";

    /// <inheritdoc />
    public override Guid Id => new Guid("4e2945d8-c6df-4613-bc75-c54d193d58ef");

    /// <inheritdoc />
    public override string Description => "Sync your library to ReTrak and scrobble your watch status.";

    /// <summary>
    /// Gets the plugin instance.
    /// </summary>
    public static Plugin Instance { get; private set; }

    /// <summary>
    /// Gets the plugin configuration.
    /// </summary>
    public PluginConfiguration PluginConfiguration => Configuration;

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "retrak",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html",
            },
            new PluginPageInfo
            {
                Name = "retrakjs",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.js"
            }
        };
    }
}
