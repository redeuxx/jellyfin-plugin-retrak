using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;
using ReTrak.Api.Enums;

namespace ReTrak.Api.DataContracts.Scrobble;

/// <summary>
/// The ReTrak scrobble response class.
/// </summary>
public class ReTrakScrobbleResponse
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the action.
    /// </summary>
    [JsonPropertyName("action")]
    public ReTrakAction Action { get; set; }

    /// <summary>
    /// Gets or sets the progress.
    /// </summary>
    [JsonPropertyName("progress")]
    public float Progress { get; set; }

    /// <summary>
    /// Gets or sets the sharing options.
    /// </summary>
    [JsonPropertyName("sharing")]
    public SocialMedia Sharing { get; set; }

    /// <summary>
    /// Gets or sets the movie.
    /// </summary>
    [JsonPropertyName("movie")]
    public ReTrakMovie Movie { get; set; }

    /// <summary>
    /// Gets or sets the episode.
    /// </summary>
    [JsonPropertyName("episode")]
    public ReTrakEpisode Episode { get; set; }

    /// <summary>
    /// Gets or sets the show.
    /// </summary>
    [JsonPropertyName("show")]
    public ReTrakShow Show { get; set; }
}
