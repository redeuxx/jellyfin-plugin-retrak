using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.Ratings;

/// <summary>
/// The ReTrak sync movie rated class.
/// </summary>
public class ReTrakMovieRated : ReTrakRated
{
    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the year.
    /// </summary>
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the ids.
    /// </summary>
    [JsonPropertyName("ids")]
    public ReTrakMovieId Ids { get; set; }
}
