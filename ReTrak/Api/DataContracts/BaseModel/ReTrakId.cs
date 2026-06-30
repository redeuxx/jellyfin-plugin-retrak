using System.Text.Json.Serialization;

namespace ReTrak.Api.DataContracts.BaseModel;

/// <summary>
/// The ReTrak id class.
/// </summary>
public class ReTrakId
{
    /// <summary>
    /// Gets or sets the ReTrak item id.
    /// </summary>
    [JsonPropertyName("ReTrak")]
    public int? ReTrak { get; set; }

    /// <summary>
    /// Gets or sets the item slug.
    /// </summary>
    [JsonPropertyName("slug")]
    public string Slug { get; set; }
}
