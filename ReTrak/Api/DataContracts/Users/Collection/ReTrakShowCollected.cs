#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Collection;

/// <summary>
/// The ReTrak users show collected class.
/// </summary>
public class ReTrakShowCollected
{
    /// <summary>
    /// Gets or sets the last collected date.
    /// </summary>
    [JsonPropertyName("last_collected_at")]
    public string LastCollectedAt { get; set; }

    /// <summary>
    /// Gets or sets the last updated date.
    /// </summary>
    [JsonPropertyName("last_updated_at")]
    public string LastUpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the show.
    /// </summary>
    [JsonPropertyName("show")]
    public ReTrakShow Show { get; set; }

    /// <summary>
    /// Gets or sets the seasons.
    /// </summary>
    [JsonPropertyName("seasons")]
    public IReadOnlyList<ReTrakSeasonCollected> Seasons { get; set; }
}
