using System.Text.Json;
using System.Text.Json.Serialization;

namespace Macadress;

/// <summary>
/// The full analysis of one address, returned by <see cref="MacadressClient.LookupAsync"/>
/// and as each item of <see cref="MacadressClient.BatchAsync"/>. A result is always
/// returned, registered or not: check <see cref="Registered"/> rather than expecting
/// an exception.
/// </summary>
public class MacResult
{
    [JsonPropertyName("mac")]
    public string Mac { get; set; } = "";

    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("oui")]
    public string? Oui { get; set; }

    [JsonPropertyName("registered")]
    public bool Registered { get; set; }

    [JsonPropertyName("organization")]
    public string? Organization { get; set; }

    [JsonPropertyName("vendor_address")]
    public string? VendorAddress { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("block_type")]
    public BlockType BlockType { get; set; }

    [JsonPropertyName("matched_prefix")]
    public string? MatchedPrefix { get; set; }

    [JsonPropertyName("prefix_length")]
    public int PrefixLength { get; set; }

    [JsonPropertyName("address_capacity")]
    public long AddressCapacity { get; set; }

    [JsonPropertyName("range_start")]
    public string? RangeStart { get; set; }

    [JsonPropertyName("range_end")]
    public string? RangeEnd { get; set; }

    [JsonPropertyName("transmission_type")]
    public TransmissionType TransmissionType { get; set; }

    [JsonPropertyName("administration_type")]
    public AdministrationType AdministrationType { get; set; }

    [JsonPropertyName("locally_administered")]
    public bool LocallyAdministered { get; set; }

    [JsonPropertyName("slap_quadrant")]
    public string? SlapQuadrant { get; set; }

    [JsonPropertyName("eui64")]
    public string? Eui64 { get; set; }

    [JsonPropertyName("ipv6_link_local")]
    public string? Ipv6LinkLocal { get; set; }

    [JsonPropertyName("potentially_randomized")]
    public bool PotentiallyRandomized { get; set; }

    [JsonPropertyName("randomization_confidence")]
    public RandomizationConfidence RandomizationConfidence { get; set; }

    /// <summary>
    /// False when the organization cannot be trusted for this address
    /// specifically (a locally administered address, a private block).
    /// </summary>
    [JsonPropertyName("vendor_lookup_reliable")]
    public bool VendorLookupReliable { get; set; }

    [JsonPropertyName("is_zero")]
    public bool IsZero { get; set; }

    [JsonPropertyName("is_broadcast")]
    public bool IsBroadcast { get; set; }

    [JsonPropertyName("explanation")]
    public string? Explanation { get; set; }

    [JsonPropertyName("device")]
    public Device Device { get; set; } = new();

    [JsonPropertyName("meta")]
    public Meta Meta { get; set; } = new();

    /// <summary>
    /// The decoded JSON object exactly as the API returned it. Fields the
    /// typed properties do not cover stay reachable through <see cref="TryGet"/>;
    /// the API only ever adds fields, so this keeps working against newer
    /// response shapes.
    /// </summary>
    [JsonIgnore]
    public JsonElement Raw { get; set; }

    /// <summary>
    /// Fetches a value from <see cref="Raw"/> by dotted path, e.g.
    /// "meta.database_version".
    /// </summary>
    public bool TryGet(string path, out JsonElement value)
    {
        var cur = Raw;
        foreach (var segment in path.Split('.'))
        {
            if (cur.ValueKind != JsonValueKind.Object || !cur.TryGetProperty(segment, out cur))
            {
                value = default;
                return false;
            }
        }
        value = cur;
        return true;
    }
}
