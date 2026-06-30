using System.Text.Json.Serialization;

namespace ReTrak.Api.DataContracts.BaseModel;

/// <summary>
/// The ReTrak show class.
/// </summary>
public class ReTrakShow
{
    /// <summary>
    /// Gets or sets the show title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the show year.
    /// </summary>
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the show ids.
    /// </summary>
    [JsonPropertyName("ids")]
    public ReTrakShowId Ids { get; set; }
}
