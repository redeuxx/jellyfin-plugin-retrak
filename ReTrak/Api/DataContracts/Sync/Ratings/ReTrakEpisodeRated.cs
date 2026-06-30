using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.Ratings;

/// <summary>
/// The ReTrak sync episode rated class.
/// </summary>
public class ReTrakEpisodeRated : ReTrakRated
{
    /// <summary>
    /// Gets or sets the episode number.
    /// </summary>
    [JsonPropertyName("number")]
    public int? Number { get; set; }

    /// <summary>
    /// Gets or sets the ids.
    /// </summary>
    [JsonPropertyName("ids")]
    public ReTrakEpisodeId Ids { get; set; }
}
