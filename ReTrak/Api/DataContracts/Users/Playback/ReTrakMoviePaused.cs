using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Playback;

/// <summary>
/// The ReTrak movie paused class.
/// </summary>
public class ReTrakMoviePaused
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the movie.
    /// </summary>
    [JsonPropertyName("movie")]
    public ReTrakMovie Movie { get; set; }

    /// <summary>
    /// Gets or sets the paused datetime.
    /// </summary>
    [JsonPropertyName("paused_at")]
    public string PausedAt { get; set; }

    /// <summary>
    /// Gets or sets the progress.
    /// </summary>
    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }
}
