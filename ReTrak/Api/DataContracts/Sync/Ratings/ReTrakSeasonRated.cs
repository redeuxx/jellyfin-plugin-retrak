#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.Ratings;

/// <summary>
/// The ReTrak sync season rated class.
/// </summary>
public class ReTrakSeasonRated : ReTrakRated
{
    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    [JsonPropertyName("number")]
    public int? Number { get; set; }

    /// <summary>
    /// Gets or sets the episodes.
    /// </summary>
    [JsonPropertyName("episodes")]
    public IReadOnlyList<ReTrakEpisodeRated> Episodes { get; set; }
}
