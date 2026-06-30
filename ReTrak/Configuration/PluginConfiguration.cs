#pragma warning disable CA1819

using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Plugins;
using ReTrak.Model;

namespace ReTrak.Configuration;

/// <summary>
/// Plugin configuration for ReTrak.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        ReTrakUsers = Array.Empty<ReTrakUser>();
        ReTrakUrl = "https://retrak.tv";
    }

    /// <summary>
    /// Gets or sets the ReTrak server URL.
    /// </summary>
    public string ReTrakUrl { get; set; }

    /// <summary>
    /// Gets or sets the ReTrak users.
    /// </summary>
    public ReTrakUser[] ReTrakUsers { get; set; }

    /// <summary>
    /// Gets a list of all configured ReTrak users.
    /// </summary>
    /// <returns>All ReTrak users.</returns>
    public IReadOnlyList<ReTrakUser> GetAllReTrakUsers()
    {
        return ReTrakUsers.ToList();
    }
}
