#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ReTrak.Api.DataContracts.Sync.Collection;

/// <summary>
/// The ReTrak sync seasons collected class.
/// </summary>
public class ReTrakSeasonCollected
{
    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    [JsonPropertyName("number")]
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets the episodes.
    /// </summary>
    [JsonPropertyName("episodes")]
    public ICollection<ReTrakEpisodeCollected> Episodes { get; set; }
}
