using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Scrobble;

/// <summary>
/// The ReTrak movie scrobble class.
/// </summary>
public class ReTrakScrobbleMovie
{
    /// <summary>
    /// Gets or sets the movie.
    /// </summary>
    [JsonPropertyName("movie")]
    public ReTrakMovie Movie { get; set; }

    /// <summary>
    /// Gets or sets the progress.
    /// </summary>
    [JsonPropertyName("progress")]
    public float Progress { get; set; }

    /// <summary>
    /// Gets or sets the app versin.
    /// </summary>
    [JsonPropertyName("app_version")]
    public string AppVersion { get; set; }

    /// <summary>
    /// Gets or sets the app date.
    /// </summary>
    [JsonPropertyName("app_date")]
    public string AppDate { get; set; }
}
