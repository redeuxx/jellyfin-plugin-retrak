using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Watched;

/// <summary>
/// The ReTrak users movie watched class.
/// </summary>
public class ReTrakMovieWatched
{
    /// <summary>
    /// Gets or sets the amount of plays.
    /// </summary>
    [JsonPropertyName("plays")]
    public int Plays { get; set; }

    /// <summary>
    /// Gets or sets the last updated date.
    /// </summary>
    [JsonPropertyName("last_updated_at")]
    public string LastUpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last watched date.
    /// </summary>
    [JsonPropertyName("last_watched_at")]
    public string LastWatchedAt { get; set; }

    /// <summary>
    /// Gets or sets the movie.
    /// </summary>
    [JsonPropertyName("movie")]
    public ReTrakMovie Movie { get; set; }
}
