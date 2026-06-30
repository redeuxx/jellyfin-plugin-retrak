#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.Ratings;

/// <summary>
/// The ReTrak sync show rated class.
/// </summary>
public class ReTrakShowRated : ReTrakRated
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
    public ReTrakShowId Ids { get; set; }

    /// <summary>
    /// Gets or sets the seasons.
    /// </summary>
    [JsonPropertyName("seasons")]
    public IReadOnlyList<ReTrakSeasonRated> Seasons { get; set; }
}
