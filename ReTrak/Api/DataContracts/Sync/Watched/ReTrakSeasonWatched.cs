#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.Watched;

/// <summary>
/// The ReTrak sync season watched class.
/// </summary>
public class ReTrakSeasonWatched : ReTrakSeason
{
    /// <summary>
    /// Gets or sets the watched date.
    /// </summary>
    [JsonPropertyName("watched_at")]
    public string WatchedAt { get; set; }

    /// <summary>
    /// Gets or sets the episodes.
    /// </summary>
    [JsonPropertyName("episodes")]
    public ICollection<ReTrakEpisodeWatched> Episodes { get; set; }
}
