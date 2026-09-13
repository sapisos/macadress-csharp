using System.Text.Json.Serialization;

namespace Macadress;

/// <summary>
/// One entry of a <see cref="MacadressClient.BatchAsync"/> response: a full
/// <see cref="MacResult"/> plus the original input string, and an
/// <see cref="Error"/> message when that one address could not be resolved.
/// </summary>
public sealed class BatchItem : MacResult
{
    [JsonPropertyName("input")]
    public string Input { get; set; } = "";

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Reports whether this entry could not be resolved. When it does, the
    /// inherited <see cref="MacResult"/> fields are unset and <see cref="Error"/>
    /// holds the reason.
    /// </summary>
    [JsonIgnore]
    public bool Failed => !string.IsNullOrEmpty(Error);
}
