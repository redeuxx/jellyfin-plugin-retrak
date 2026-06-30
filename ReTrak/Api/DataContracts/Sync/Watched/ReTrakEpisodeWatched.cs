using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.Watched;

/// <summary>
/// The ReTrak sync episode watched class.
/// </summary>
public class ReTrakEpisodeWatched : ReTrakEpisode
{
    /// <summary>
    /// Gets or sets the watched date.
    /// </summary>
    [JsonPropertyName("watched_at")]
    public string WatchedAt { get; set; }
}
