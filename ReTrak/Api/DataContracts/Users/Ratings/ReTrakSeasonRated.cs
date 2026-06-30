using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Ratings;

/// <summary>
/// The ReTrak users season rated class.
/// </summary>
public class ReTrakSeasonRated : ReTrakRated
{
    /// <summary>
    /// Gets or sets the season.
    /// </summary>
    [JsonPropertyName("season")]
    public ReTrakSeason Season { get; set; }
}
