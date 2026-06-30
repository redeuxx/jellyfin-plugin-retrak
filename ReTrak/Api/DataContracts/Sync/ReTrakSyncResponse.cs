#nullable enable

using System.Text.Json.Serialization;

namespace ReTrak.Api.DataContracts.Sync;

/// <summary>
/// The ReTrak sync response class.
/// </summary>
public class ReTrakSyncResponse
{
    /// <summary>
    /// Gets or sets the added items.
    /// </summary>
    [JsonPropertyName("added")]
    public Items? Added { get; set; }

    /// <summary>
    /// Gets or sets the deleted items.
    /// </summary>
    [JsonPropertyName("deleted")]
    public Items? Deleted { get; set; }

    /// <summary>
    /// Gets or sets the updated items.
    /// </summary>
    [JsonPropertyName("updated")]
    public Items? Updated { get; set; }

    /// <summary>
    /// Gets or sets the not found items.
    /// </summary>
    [JsonPropertyName("not_found")]
    public NotFoundObjects? NotFound { get; set; }
}
