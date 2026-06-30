using System.Text.Json.Serialization;

namespace ReTrak.Api.DataContracts.BaseModel;

/// <summary>
/// The ReTrak season class.
/// </summary>
public class ReTrakSeason
{
    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    [JsonPropertyName("number")]
    public int? Number { get; set; }

    /// <summary>
    /// Gets or sets the season ids.
    /// </summary>
    [JsonPropertyName("ids")]
    public ReTrakSeasonId Ids { get; set; }
}
