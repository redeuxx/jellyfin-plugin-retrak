using System.Text.Json.Serialization;
using ReTrak.Api.Enums;

namespace ReTrak.Api.DataContracts.Users.Collection;

/// <summary>
/// The ReTrak users metadata class.
/// </summary>
public class ReTrakMetadata
{
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
    /// Gets or sets a value indicating whether the movie is 3D.
    /// </summary>
    [JsonPropertyName("3d")]
    public bool? Is3D { get; set; } = false;

    /// <summary>
    /// Gets or sets the HDR type.
    /// </summary>
    [JsonPropertyName("hdr")]
    public ReTrakHdr? Hdr { get; set; }
}
