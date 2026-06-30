#nullable enable

using System.Text.Json.Serialization;

namespace ReTrak.Api.DataContracts.BaseModel;

/// <summary>
/// The ReTrak tv id class.
/// </summary>
public class ReTrakTVId : ReTrakIMDBandTMDBId
{
    /// <summary>
    /// Gets or sets the TVDb id.
    /// </summary>
    [JsonPropertyName("tvdb")]
    public string? Tvdb { get; set; }

    /// <summary>
    /// Gets or sets the TVRage id.
    /// </summary>
    [JsonPropertyName("tvrage")]
    public string? Tvrage { get; set; }
}
