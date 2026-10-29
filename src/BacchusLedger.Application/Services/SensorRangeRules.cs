using BacchusLedger.Application.Models;
using BacchusLedger.Application.Providers;
using BacchusLedger.Domain.Ranges;

namespace BacchusLedger.Application.Services;

/// One RangeTracker per vessel, in memory (like the simulator's), guarded for the outbox worker.
public sealed class SensorRangeRules(IRangeConfigSource ranges) : ISensorRangeRules
{
    readonly RangeConfig config = new(ranges.Config);
    readonly Dictionary<long, RangeTracker> trackers = [];
    readonly object gate = new();

    public IReadOnlyList<LiveEvent> Check(LiveEvent reading, VesselRecord vessel, string? batchCode)
    {
        if (reading.Metric is not { } metric) return [];
        // the sensor sends dips (level); level ranges do not depend on fermentation or wine colour
        var range = config.For(vessel.WineryCode, vessel.Code, metric, false, "red", vessel.CapacityL);
        var name = reading.VesselName ?? vessel.Code;
        lock (gate)
        {
            if (!trackers.TryGetValue(vessel.Id, out var tracker)) trackers[vessel.Id] = tracker = new RangeTracker();
            var (change, dir) = tracker.Check(metric, reading.Value, range, vessel.CapacityL, reading.SimTime);
            if (change == RangeTracker.Change.Fire)
                return [Note(reading, "red", (metric == "temperature" ? "temp" : metric) + (dir == "too low" ? "_low" : "_high"),
                    $"{name}{(batchCode != null ? $" ({batchCode})" : "")}: {metric} {dir} — {RangeRule.Format(reading.Value!.Value)} {range!.Unit} " +
                    $"(allowed {RangeRule.Format(range.Min)}–{RangeRule.Format(range.Max)} {range.Unit}, {RangeTracker.InARow} readings in a row).")];
            if (change == RangeTracker.Change.Clear && tracker.Alerts.Count == 0)
                return [Note(reading, "green", "back_normal", $"{name} back to normal.")];
            return [];
        }
    }

    static LiveEvent Note(LiveEvent reading, string level, string rule, string message) => new()
    {
        Kind = "notification", Level = level, Rule = rule, Message = message, VesselId = reading.VesselId, VesselCode = reading.VesselCode,
        VesselName = reading.VesselName, CreatedAt = reading.CreatedAt, SimTime = reading.SimTime, Source = "SENSOR",
    };
}
