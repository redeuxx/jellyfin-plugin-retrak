using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.History;

/// <summary>
/// The ReTrak sync episode watched history class.
/// </summary>
public class ReTrakEpisodeWatchedHistory
{
    /// <summary>
    /// Gets or sets the watched date.
    /// </summary>
    [JsonPropertyName("watched_at")]
    public string WatchedAt { get; set; }

    /// <summary>
    /// Gets or sets the action.
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the episode.
    /// </summary>
    [JsonPropertyName("episode")]
    public ReTrakEpisode Episode { get; set; }

    /// <summary>
    /// Gets or sets the episode.
    /// </summary>
    [JsonPropertyName("show")]
    public ReTrakShow Show { get; set; }
}
