using System.Text.Json.Serialization;

namespace Macadress;

/// <summary>Per-response bookkeeping.</summary>
public sealed class Meta
{
    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }

    [JsonPropertyName("database_version")]
    public string? DatabaseVersion { get; set; }

    [JsonPropertyName("cached")]
    public bool Cached { get; set; }
}
