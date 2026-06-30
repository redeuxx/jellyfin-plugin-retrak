using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Ratings;

/// <summary>
/// The ReTrak users show rated class.
/// </summary>
public class ReTrakShowRated : ReTrakRated
{
    /// <summary>
    /// Gets or sets the show.
    /// </summary>
    [JsonPropertyName("show")]
    public ReTrakShow Show { get; set; }
}
