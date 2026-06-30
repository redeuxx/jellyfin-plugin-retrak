#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.Watched;

/// <summary>
/// The ReTrak sync show watched class.
/// </summary>
public class ReTrakShowWatched : ReTrakShow
{
    /// <summary>
    /// Gets or sets the watched date.
    /// </summary>
    [JsonPropertyName("watched_at")]
    public string WatchedAt { get; set; }

    /// <summary>
    /// Gets or sets the seasons.
    /// </summary>
    [JsonPropertyName("seasons")]
    public ICollection<ReTrakSeasonWatched> Seasons { get; set; }
}
