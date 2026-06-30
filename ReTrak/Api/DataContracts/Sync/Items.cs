using System.Text.Json.Serialization;

namespace ReTrak.Api.DataContracts.Sync;

/// <summary>
/// The ReTrak sync items class.
/// </summary>
public class Items
{
    /// <summary>
    /// Gets or sets the movies.
    /// </summary>
    [JsonPropertyName("movies")]
    public int Movies { get; set; }

    /// <summary>
    /// Gets or sets the episodes.
    /// </summary>
    [JsonPropertyName("episodes")]
    public int Episodes { get; set; }
}
