using System.Text.Json;
using System.Text.Json.Serialization;

namespace Macadress;

/// <summary>
/// The result of <see cref="MacadressClient.SearchVendorsAsync"/>: the
/// matching blocks for this page plus the total match count, which ignores
/// the limit.
/// </summary>
public sealed class VendorSearchResult
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("blocks")]
    public List<VendorBlock> Blocks { get; set; } = new();

    /// <summary>The decoded JSON object exactly as the API returned it.</summary>
    [JsonIgnore]
    public JsonElement Raw { get; set; }
}
