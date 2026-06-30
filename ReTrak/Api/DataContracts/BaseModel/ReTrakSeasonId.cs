using System.Text.Json.Serialization;

namespace ReTrak.Api.DataContracts.BaseModel;

/// <summary>
/// The ReTrak season id class.
/// </summary>
public class ReTrakSeasonId : ReTrakId
{
    /// <summary>
    /// Gets or sets the season TMDb id.
    /// </summary>
    [JsonPropertyName("tmdb")]
    public int? Tmdb { get; set; }

    /// <summary>
    /// Gets or sets the season TVDb id.
    /// </summary>
    [JsonPropertyName("tvdb")]
    public int? Tvdb { get; set; }

    /// <summary>
    /// Gets or sets the season TVRage id.
    /// </summary>
    [JsonPropertyName("tvrage")]
    public int? Tvrage { get; set; }
}
