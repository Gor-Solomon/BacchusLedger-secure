using System.Globalization;
using System.Text.Json;

namespace BacchusLedger.Extensions;

/// Reading loosely typed request bodies (numbers may arrive as strings).
public static class JsonElementExtensions
{
    /// A number, or a string holding a number; otherwise null.
    public static double? Num(this JsonElement b, string k) => b.ValueKind == JsonValueKind.Object && b.TryGetProperty(k, out var v) ? v.ValueKind switch
    {
        JsonValueKind.Number => v.GetDouble(),
        JsonValueKind.String when double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
        _ => null,
    } : null;

    /// A string value, trimmed; null when missing or not a string.
    public static string? Str(this JsonElement b, string k) =>
        b.ValueKind == JsonValueKind.Object && b.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() : null;

    /// A non-empty string value as sent (not trimmed); null when missing, empty or not a string.
    public static string? NonEmptyStr(this JsonElement b, string k) =>
        b.ValueKind == JsonValueKind.Object && b.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } x ? x : null;

    /// A string value as sent (not trimmed); null when missing or not a string.
    public static string? RawStr(this JsonElement b, string k) =>
        b.ValueKind == JsonValueKind.Object && b.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// True only for a JSON true.
    public static bool Flag(this JsonElement b, string k) => b.ValueKind == JsonValueKind.Object && b.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;

    public static bool Has(this JsonElement b, string k) => b.ValueKind == JsonValueKind.Object && b.TryGetProperty(k, out _);
}
