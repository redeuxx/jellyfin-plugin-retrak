using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Scrobble;

/// <summary>
/// The ReTrak episode scrobble class.
/// </summary>
public class ReTrakScrobbleEpisode
{
    /// <summary>
    /// Gets or sets the show.
    /// </summary>
    [JsonPropertyName("show")]
    public ReTrakShow Show { get; set; }

    /// <summary>
    /// Gets or sets the episode.
    /// </summary>
    [JsonPropertyName("episode")]
    public ReTrakEpisode Episode { get; set; }

    /// <summary>
    /// Gets or sets the progress.
    /// </summary>
    [JsonPropertyName("progress")]
    public float Progress { get; set; }

    /// <summary>
    /// Gets or sets the app version.
    /// </summary>
    [JsonPropertyName("app_version")]
    public string AppVersion { get; set; }

    /// <summary>
    /// Gets or sets the app date.
    /// </summary>
    [JsonPropertyName("app_date")]
    public string AppDate { get; set; }
}
