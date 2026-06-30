using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Ratings;

/// <summary>
/// The ReTrak users movie rated class.
/// </summary>
public class ReTrakMovieRated : ReTrakRated
{
    /// <summary>
    /// Gets or sets the movie.
    /// </summary>
    [JsonPropertyName("movie")]
    public ReTrakMovie Movie { get; set; }
}
