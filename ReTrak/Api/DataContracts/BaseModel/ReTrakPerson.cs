using System.Text.Json.Serialization;

namespace ReTrak.Api.DataContracts.BaseModel;

/// <summary>
/// The ReTrak person class.
/// </summary>
public class ReTrakPerson
{
    /// <summary>
    /// Gets or sets the person name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the person ids.
    /// </summary>
    [JsonPropertyName("ids")]
    public ReTrakPersonId Ids { get; set; }
}
