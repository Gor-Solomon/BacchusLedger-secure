using System.Text.Json;
using System.Text.Json.Nodes;

namespace BacchusLedger.Domain.Ranges;

/// Range alerts for the Live board: allowed min/max per measurement from config/ranges.json (DEMO ASSUMPTIONS).
/// A per-vessel entry wins over the defaults. Plain code only, no AI.
public sealed class RangeConfig(JsonNode? config)
{
    static readonly Dictionary<string, string> Units = new() { ["temperature"] = "°C", ["ph"] = "pH", ["density"] = "SG", ["level"] = "L", ["ta"] = "g/L" };

    /// Red and qvevri amber wine use the red fermentation range; only a wine type that says "white" uses the white range.
    public static string WineColorOf(string? wineType) => (wineType ?? "").Contains("white", StringComparison.OrdinalIgnoreCase) ? "white" : "red";

    /// Allowed range for one measurement of one vessel, or null when the file has none (then no range alert).
    public RangeRule? For(string winery, string code, string metric, bool fermenting, string wineColor, double? capacity)
    {
        if (config == null) return null;
        var unit = Units.GetValueOrDefault(metric, "");
        foreach (var key in new[] { $"{winery}/{code}", code })
            if (Make(config["vessels"]?[key]?[metric], capacity, $"override {key}", unit) is { } own) return own;
        var def = metric == "temperature"
            ? fermenting ? (wineColor == "white" ? "temperature_fermenting_white" : "temperature_fermenting_red") : "temperature_storage"
            : metric;
        return Make(config["defaults"]?[def], capacity, $"default {def}", unit);
    }

    static double? Num(JsonNode? n, double? capacity) => n switch
    {
        null => null,
        JsonValue v when v.GetValueKind() == JsonValueKind.Number => v.GetValue<double>(),
        JsonValue v when v.GetValueKind() == JsonValueKind.String && v.GetValue<string>() == "capacity" => capacity,
        _ => null,
    };

    static RangeRule? Make(JsonNode? n, double? capacity, string from, string unit) =>
        Num(n?["min"], capacity) is { } min && Num(n?["max"], capacity) is { } max && min <= max
            ? new(min, max, n?["unit"] is JsonValue u && u.GetValueKind() == JsonValueKind.String ? u.GetValue<string>() : unit, from) : null;
}
