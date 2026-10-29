namespace BacchusLedger.Domain.Ranges;

/// Per vessel: counts out-of-range readings in a row per measurement. Fires once after 3 in a row, never repeats while
/// still out of range, clears on the first reading back in range.
public sealed class RangeTracker
{
    public const int InARow = 3;
    public enum Change { None, Fire, Clear }
    public sealed record Active(string Metric, string Direction, double Value, RangeRule Range, string Since);

    readonly Dictionary<string, int> streak = [];
    public Dictionary<string, Active> Alerts { get; } = [];
    /// Last checked state per measurement: true = in range, false = out of range (for the green/red colours).
    public Dictionary<string, bool> InRange { get; } = [];

    public (Change change, string direction) Check(string metric, double? value, RangeRule? range, double? capacity, string time)
    {
        if (range == null || !Plausible(metric, value, capacity)) return (Change.None, "");
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

    /// Physically possible values. Anything else (empty, NaN, wrong unit, sensor glitch) is skipped: no alert, no count.
    static bool Plausible(string metric, double? value, double? capacity) => value is { } x && double.IsFinite(x) && metric switch
    {
        "temperature" => x is >= -10 and <= 50,
        "density" => x is >= 0.9 and <= 1.25,
        "ph" => x is >= 0 and <= 14,
        "ta" => x is >= 0 and <= 30,
        "level" => x >= 0 && (capacity == null || x <= capacity * 1.5),
        _ => true,
    };
}
