using System.Globalization;
using System.Text.Json.Nodes;

namespace BacchusLedger;

/// Range alerts for the Live board: allowed min/max per measurement from config/ranges.json (DEMO ASSUMPTIONS).
/// Plain code only — no AI. A per-vessel entry in the file wins over the defaults.
public static class Ranges
{
    public sealed record Range(double Min, double Max, string Unit, string From);

    /// Red and qvevri amber wine use the red fermentation range; only a wine type that says "white" uses the white range.
    public static string WineColor(string? wineType) => (wineType ?? "").Contains("white", StringComparison.OrdinalIgnoreCase) ? "white" : "red";

    static double? Num(JsonNode? n, double? capacity) => n switch
    {
        null => null,
        JsonValue v when v.GetValueKind() == System.Text.Json.JsonValueKind.Number => v.GetValue<double>(),
        JsonValue v when v.GetValueKind() == System.Text.Json.JsonValueKind.String && v.GetValue<string>() == "capacity" => capacity,
        _ => null,
    };

    static Range? Make(JsonNode? n, double? capacity, string from, string unit) =>
        Num(n?["min"], capacity) is { } min && Num(n?["max"], capacity) is { } max && min <= max
            ? new(min, max, n?["unit"] is JsonValue u && u.GetValueKind() == System.Text.Json.JsonValueKind.String ? u.GetValue<string>() : unit, from) : null;

    static readonly Dictionary<string, string> Units = new() { ["temperature"] = "°C", ["ph"] = "pH", ["density"] = "SG", ["level"] = "L", ["ta"] = "g/L" };

    /// Allowed range for one measurement of one vessel, or null when the file has none (then no range alert).
    public static Range? For(JsonNode? cfg, string winery, string code, string metric, bool fermenting, string wineColor, double? capacity)
    {
        if (cfg == null) return null;
        var unit = Units.GetValueOrDefault(metric, "");
        foreach (var key in new[] { $"{winery}/{code}", code })
            if (Make(cfg["vessels"]?[key]?[metric], capacity, $"override {key}", unit) is { } own) return own;
        var def = metric == "temperature"
            ? fermenting ? (wineColor == "white" ? "temperature_fermenting_white" : "temperature_fermenting_red") : "temperature_storage"
            : metric;
        return Make(cfg["defaults"]?[def], capacity, $"default {def}", unit);
    }

    /// Physically possible values. Anything else (empty, NaN, wrong unit, sensor glitch) is skipped: no alert, no count.
    public static bool Plausible(string metric, double? value, double? capacity) => value is { } x && double.IsFinite(x) && metric switch
    {
        "temperature" => x is >= -10 and <= 50,
        "density" => x is >= 0.9 and <= 1.25,
        "ph" => x is >= 0 and <= 14,
        "ta" => x is >= 0 and <= 30,
        "level" => x >= 0 && (capacity == null || x <= capacity * 1.5),
        _ => true,
    };

    public static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}

/// Per vessel: counts out-of-range readings in a row per measurement. Fires once after 3 in a row, never repeats while
/// still out of range, clears on the first reading back in range.
public sealed class RangeTracker
{
    public const int InARow = 3;
    public enum Change { None, Fire, Clear }
    public sealed record Active(string Metric, string Direction, double Value, Ranges.Range Range, string Since);

    readonly Dictionary<string, int> streak = [];
    public Dictionary<string, Active> Alerts { get; } = [];
    /// Last checked state per measurement: true = in range, false = out of range (for the green/red colours).
    public Dictionary<string, bool> InRange { get; } = [];

    public (Change change, string direction) Check(string metric, double? value, Ranges.Range? range, double? capacity, string time)
    {
        if (range == null || !Ranges.Plausible(metric, value, capacity)) return (Change.None, "");
        double x = value!.Value;
        string dir = x > range.Max ? "too high" : x < range.Min ? "too low" : "";
        InRange[metric] = dir == "";
        if (dir == "")
        {
            streak[metric] = 0;
            return Alerts.Remove(metric) ? (Change.Clear, "") : (Change.None, "");
        }
        streak[metric] = streak.GetValueOrDefault(metric) + 1;
        if (Alerts.TryGetValue(metric, out var a)) { Alerts[metric] = a with { Value = x }; return (Change.None, dir); }
        if (streak[metric] < InARow) return (Change.None, dir);
        Alerts[metric] = new Active(metric, dir, x, range, time);
        return (Change.Fire, dir);
    }

    public void Reset() { streak.Clear(); Alerts.Clear(); InRange.Clear(); }
}
