using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;
using ReTrak.Api.Enums;

namespace ReTrak.Api.DataContracts.Sync.Collection;

/// <summary>
/// The ReTrak sync episodes collected class.
/// </summary>
public class ReTrakEpisodeCollected : ReTrakEpisode
{
    /// <summary>
    /// Gets or sets the colletion date.
    /// </summary>
    [JsonPropertyName("collected_at")]
    public string CollectedAt { get; set; }

    /// <summary>
    /// Gets or sets the media type.
    /// </summary>
    [JsonPropertyName("media_type")]
    public ReTrakMediaType? MediaType { get; set; }

    /// <summary>
    /// Gets or sets the resolution.
    /// </summary>
    [JsonPropertyName("resolution")]
    public ReTrakResolution? Resolution { get; set; }

    /// <summary>
    /// Gets or sets the audio.
    /// </summary>
    [JsonPropertyName("audio")]
    public ReTrakAudio? Audio { get; set; }

    /// <summary>
    /// Gets or sets the amount of audio channels.
    /// </summary>
    [JsonPropertyName("audio_channels")]
    public string AudioChannels { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the episode is 3D.
    /// </summary>
    [JsonPropertyName("3d")]
    public bool? Is3D { get; set; } = false;

    /// <summary>
    /// Gets or sets the HDR type.
    /// </summary>
    [JsonPropertyName("hdr")]
    public ReTrakHdr? Hdr { get; set; }
}
