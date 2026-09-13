using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Macadress;

/// <summary>
/// Maps <see cref="BlockType"/> to/from the API's hyphenated block names
/// (e.g. "MA-L"). An unrecognised value decodes to <see cref="BlockType.Unknown"/>
/// instead of throwing, so a newer API version keeps working.
/// </summary>
internal sealed class BlockTypeConverter : JsonConverter<BlockType>
{
    public override BlockType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetString() switch
        {
            "MA-L" => BlockType.MAL,
            "MA-M" => BlockType.MAM,
            "MA-S" => BlockType.MAS,
            "IAB" => BlockType.IAB,
            "CID" => BlockType.CID,
            _ => BlockType.Unknown,
        };
    }

    public override void Write(Utf8JsonWriter writer, BlockType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            BlockType.MAL => "MA-L",
            BlockType.MAM => "MA-M",
            BlockType.MAS => "MA-S",
            BlockType.IAB => "IAB",
            BlockType.CID => "CID",
            _ => "unknown",
        });
    }
}

/// <summary>
/// Maps an enum to/from the API's snake_case string values by splitting the
/// member name on capital letters (e.g. <c>WirelessAccessPoint</c> &lt;-&gt;
/// "wireless_access_point"). An unrecognised value decodes to the enum's
/// zero value.
/// </summary>
internal sealed class SnakeCaseEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        if (raw is null || raw.Length == 0)
        {
            return default;
        }
        var pascal = SnakeToPascal(raw);
        return Enum.TryParse<TEnum>(pascal, ignoreCase: true, out var value) ? value : default;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(PascalToSnake(value.ToString()));
    }

    private static string SnakeToPascal(string snake)
    {
        var sb = new StringBuilder();
        foreach (var part in snake.Split('_'))
        {
            if (part.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1) sb.Append(part.Substring(1).ToLowerInvariant());
        }
        return sb.ToString();
    }

    private static string PascalToSnake(string pascal)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < pascal.Length; i++)
        {
            var c = pascal[i];
            if (char.IsUpper(c) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
