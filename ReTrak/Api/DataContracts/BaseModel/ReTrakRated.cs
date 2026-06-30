using System.Text.Json.Serialization;

namespace ReTrak.Api.DataContracts.BaseModel;

/// <summary>
/// The ReTrak rated class.
/// </summary>
public abstract class ReTrakRated
{
    /// <summary>
    /// Gets or sets the rating.
    /// </summary>
    [JsonPropertyName("rating")]
    public int? Rating { get; set; }

    /// <summary>
    /// Gets or sets the rating date.
    /// </summary>
    [JsonPropertyName("rated_at")]
    public string RatedAt { get; set; }
}
