using System.Globalization;
using System.Text.Json.Nodes;

namespace BacchusLedger;

/// Range alerts for the Live board: allowed min/max per measurement from config/ranges.json (DEMO ASSUMPTIONS).
/// Plain code only — no AI. A per-vessel entry in the file wins over the defaults.
public static class Ranges
{
    public sealed record Range(double Min, double Max, string Unit, string From);

    public static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "config", "ranges.json");
    static readonly Lazy<JsonNode?> File = new(() =>
    {
        try { return System.IO.File.Exists(ConfigPath) ? JsonNode.Parse(System.IO.File.ReadAllText(ConfigPath)) : null; }
        catch (Exception e) { Console.Error.WriteLine($"ranges: config/ranges.json unreadable ({e.GetType().Name}) — no range alerts"); return null; }
    });
    public static JsonNode? Config => File.Value;

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

/// Selftest: range alert rules (plain code, run on a simulator in a rolled-back transaction).
public static class RangeSelfTest
{
    public static void Run(Action<string, string, string, string, bool> Add)
    {
        const string g = "range alerts";
        try
        {
            var cfg = Ranges.Config;
            string R(string metric, bool ferm, string color) => Ranges.For(cfg, "W01", "X-0", metric, ferm, color, 1000) is { } r ? $"{Ranges.Fmt(r.Min)}–{Ranges.Fmt(r.Max)}" : "none";
            var act = $"red {R("temperature", true, "red")}, white {R("temperature", true, "white")}, storage {R("temperature", false, "red")}, pH {R("ph", false, "red")}";
            const string exp = "red 20–30, white 12–20, storage 10–18, pH 3–4";
            Add(g, "config/ranges.json defaults (fermenting red/amber, white, storage, pH)", exp, act, act == exp);
            var amber = Ranges.WineColor("Qvevri amber wine");
            Add(g, "Qvevri amber wine uses the red fermentation range", "red", amber, amber == "red" && Ranges.WineColor("Classic white wine") == "white");

            var own = System.Text.Json.Nodes.JsonNode.Parse("""{"defaults":{"temperature_fermenting_red":{"min":20,"max":30}},"vessels":{"W01/F-2":{"temperature":{"min":22,"max":28}}}}""");
            var o = Ranges.For(own, "W01", "F-2", "temperature", true, "red", null);
            var d = Ranges.For(own, "W01", "F-1", "temperature", true, "red", null);
            Add(g, "Per-vessel override wins over the default", "F-2 22–28, F-1 20–30",
                $"F-2 {o?.Min}–{o?.Max}, F-1 {d?.Min}–{d?.Max}", o is { Min: 22, Max: 28 } && d is { Min: 20, Max: 30 });

            // 3 in a row, no repeat, clear when back in range; invalid values neither count nor reset
            var t = new RangeTracker();
            var range = new Ranges.Range(20, 30, "°C", "test");
            double?[] seq = [31, 31, 25, 31, null, 31, double.NaN, 999, 31, 32, 33, 25, 19, 18, 17];
            var log = seq.Select((v, i) => t.Check("temperature", v, range, null, $"t{i}")).Select(x => x.change == RangeTracker.Change.None ? "." : x.change == RangeTracker.Change.Fire ? "F" : "C").Aggregate("", (a, x) => a + x);
            const string expLog = "........F..C..F";
            Add(g, "Alert after 3 out-of-range readings in a row, no repeat, clear when back in range, empty/impossible values skipped", expLog, log, log == expLog);

            // Simulate spike (= the "Hot fermentation" scenario) on the real simulator, rolled back
            var (fires, before, onlyLive) = Db.Probe((c, tx) =>
            {
                string Counts() => string.Join(",", new[] { "operations", "readings", "lab", "batches", "vessels" }.Select(x => Db.Query(c, $"SELECT COUNT(*) n FROM {x}")[0]["n"]));
                var c0 = Counts();
                var sim = new LiveSim(seed: 7) { On = true };
                sim.Start(c, new Dictionary<string, string>(Db.DefaultSettings));
                sim.SetScenario(c, "hot");
                int fired = 0, outBefore = 0, run = 0;
                var f2 = sim.Vessels.First(v => v.Code == "F-2");
                for (int i = 0; i < 15; i++)
                    foreach (var e in sim.Tick(c, tx).Where(e => e.VesselCode == "F-2"))
                    {
                        if (e.Kind == "reading" && e.Metric == "temperature") run = e.Value > f2.Ranges["temperature"].Max ? run + 1 : 0;
                        if (e.Kind == "notification" && e.Rule == "temp_high") { fired++; if (fired == 1) outBefore = run; }
                    }
                return (fired, outBefore, Counts() == c0);
            });
            Add(g, "Simulate spike: F-2 too hot fires exactly once, after ≥3 out-of-range readings", "1 alert after ≥3", $"{fires} alert(s) after {before}", fires == 1 && before >= RangeTracker.InARow);
            Add(g, "Simulate spike writes no record rows (only SIMULATED live_events)", "unchanged", onlyLive ? "unchanged" : "changed", onlyLive);
        }
        catch (Exception e) { Add(g, "range alerts", "ok", e.Message, false); }
    }
}
