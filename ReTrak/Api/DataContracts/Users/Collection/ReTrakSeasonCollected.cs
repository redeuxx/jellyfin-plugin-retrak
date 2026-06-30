#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ReTrak.Api.DataContracts.Users.Collection;

/// <summary>
/// The ReTrak users season collected class.
/// </summary>
public class ReTrakSeasonCollected
{
    /// <summary>
    /// Gets or sets the season unumber.
    /// </summary>
    [JsonPropertyName("number")]
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets the episodes.
    /// </summary>
    [JsonPropertyName("episodes")]
    public IReadOnlyList<ReTrakEpisodeCollected> Episodes { get; set; }
}
