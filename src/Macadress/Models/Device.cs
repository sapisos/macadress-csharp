using System.Text.Json;
using System.Text.Json.Serialization;

namespace Macadress;

/// <summary>
/// The device block of a lookup result. A MAC address alone rarely
/// determines a device type, so <see cref="Category"/> is
/// <see cref="DeviceCategory.Unknown"/> for most registrations and
/// <see cref="ExactModelKnown"/> is effectively always false.
/// </summary>
public sealed class Device
{
    [JsonPropertyName("category")]
    public DeviceCategory Category { get; set; }

    [JsonPropertyName("possible_categories")]
    public List<DeviceCategory> PossibleCategories { get; set; } = new();

    [JsonPropertyName("confidence")]
    public string? Confidence { get; set; }

    [JsonPropertyName("inference_source")]
    public string? InferenceSource { get; set; }

    [JsonPropertyName("exact_model_known")]
    public bool ExactModelKnown { get; set; }

    /// <summary>The decoded JSON object exactly as the API returned it.</summary>
    [JsonIgnore]
    public JsonElement Raw { get; set; }
}
