#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.Collection;

/// <summary>
/// The ReTrak sync show collected class.
/// </summary>
public class ReTrakShowCollected : ReTrakShow
{
    /// <summary>
    /// Gets or sets the seasons.
    /// </summary>
    [JsonPropertyName("seasons")]
    public ICollection<ReTrakSeasonCollected> Seasons { get; set; }
}
