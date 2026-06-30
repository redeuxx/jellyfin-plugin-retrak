using System.Text.Json.Serialization;

namespace ReTrak.Api.DataContracts.Scrobble;

/// <summary>
/// The ReTrak social media class.
/// </summary>
public class SocialMedia
{
    /// <summary>
    /// Gets or sets a value indicating whether twittwe posting should be enabled.
    /// </summary>
    [JsonPropertyName("twitter")]
    public bool Twitter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether tumblr posting should be enabled.
    /// </summary>
    [JsonPropertyName("tumblr")]
    public bool Tumblr { get; set; }
}
