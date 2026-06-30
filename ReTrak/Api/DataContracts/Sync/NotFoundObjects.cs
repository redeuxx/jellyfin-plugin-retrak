#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync;

/// <summary>
/// The ReTrak sync not found objects class.
/// </summary>
public class NotFoundObjects
{
    /// <summary>
    /// Gets or sets the movies.
    /// </summary>
    [JsonPropertyName("movies")]
    public IReadOnlyList<ReTrakMovie> Movies { get; set; }

    /// <summary>
    /// Gets or sets the shows.
    /// </summary>
    [JsonPropertyName("shows")]
    public IReadOnlyList<ReTrakShow> Shows { get; set; }

    /// <summary>
    /// Gets or sets the episodes.
    /// </summary>
    [JsonPropertyName("episodes")]
    public IReadOnlyList<ReTrakEpisode> Episodes { get; set; }

    /// <summary>
    /// Gets or sets the seasons.
    /// </summary>
    [JsonPropertyName("seasons")]
    public IReadOnlyList<ReTrakSeason> Seasons { get; set; }

    /// <summary>
    /// Gets or sets the people.
    /// </summary>
    [JsonPropertyName("people")]
    public IReadOnlyList<ReTrakPerson> People { get; set; }
}
