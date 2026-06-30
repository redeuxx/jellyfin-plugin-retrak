using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Ratings;

/// <summary>
/// The ReTrak users rating class.
/// </summary>
public class ReTrakEpisodeRated : ReTrakRated
{
    /// <summary>
    /// Gets or sets the episode.
    /// </summary>
    [JsonPropertyName("episode")]
    public ReTrakEpisode Episode { get; set; }
}
