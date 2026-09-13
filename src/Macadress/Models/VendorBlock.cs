using System.Text.Json.Serialization;

namespace Macadress;

/// <summary>One IEEE-assigned MAC/OUI prefix block from <see cref="MacadressClient.SearchVendorsAsync"/>.</summary>
public sealed class VendorBlock
{
    [JsonPropertyName("prefix_int")]
    public long PrefixInt { get; set; }

    [JsonPropertyName("mask_bits")]
    public int MaskBits { get; set; }

    [JsonPropertyName("block_type")]
    public BlockType BlockType { get; set; }

    [JsonPropertyName("organization")]
    public string? Organization { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("is_private")]
    public bool IsPrivate { get; set; }

    [JsonPropertyName("first_seen_at")]
    public string? FirstSeenAt { get; set; }

    [JsonPropertyName("last_changed_at")]
    public string? LastChangedAt { get; set; }
}
