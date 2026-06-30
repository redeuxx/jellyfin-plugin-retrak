using System.Text.Json.Serialization;

namespace ReTrak.Api.DataContracts.BaseModel;

/// <summary>
/// The ReTrak person id class.
/// </summary>
public class ReTrakPersonId : ReTrakIMDBandTMDBId
{
    /// <summary>
    /// Gets or sets the TVRage person id.
    /// </summary>
    [JsonPropertyName("tvrage")]
    public int? Tvrage { get; set; }
}
