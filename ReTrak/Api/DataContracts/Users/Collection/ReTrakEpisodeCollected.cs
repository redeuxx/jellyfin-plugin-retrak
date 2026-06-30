using System.Text.Json.Serialization;

namespace ReTrak.Api.DataContracts.Users.Collection;

/// <summary>
/// The ReTrak users episode collected class.
/// </summary>
public class ReTrakEpisodeCollected
{
    /// <summary>
    /// Gets or sets the episode number.
    /// </summary>
    [JsonPropertyName("number")]
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets the collection date.
    /// </summary>
    [JsonPropertyName("collected_at")]
    public string CollectedAt { get; set; }

    /// <summary>
    /// Gets or sets the metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public ReTrakMetadata Metadata { get; set; }
}
