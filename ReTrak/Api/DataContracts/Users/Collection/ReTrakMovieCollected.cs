using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Collection;

/// <summary>
/// The ReTrak users movie collected class.
/// </summary>
public class ReTrakMovieCollected
{
    /// <summary>
    /// Gets or sets the last collection date.
    /// </summary>
    [JsonPropertyName("collected_at")]
    public string CollectedAt { get; set; }

    /// <summary>
    /// Gets or sets the metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public ReTrakMetadata Metadata { get; set; }

    /// <summary>
    /// Gets or sets the movie.
    /// </summary>
    [JsonPropertyName("movie")]
    public ReTrakMovie Movie { get; set; }

    /// <summary>
    /// Gets or sets the updated date.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; }
}
