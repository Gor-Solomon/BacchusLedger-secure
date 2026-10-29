using System.Globalization;
using System.Text.Json.Nodes;
using BacchusLedger.Application.Contracts.Live;
using BacchusLedger.Domain.Dataset;
using BacchusLedger.Application.Extensions;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Application.Services;
using BacchusLedger.Domain.Ranges;

namespace BacchusLedger.Infrastructure.Live;

/// LIVE mode: everything here is SIMULATED, stored only in live_events, and never read by journals, the loss engine or ROI numbers.
public sealed class LiveSimulator(
    IBatchRepository batches, IVesselRepository vessels, IReadingRepository readings, IOperationRepository operations, ILiveEventRepository events,
    ISettingsRepository settingsRepository, ILossAnalysisService losses, RangeConfig rangeConfig, int? seed) : ILiveSimulator
{
    public sealed class VState
    {
        public long Id { get; set; }
        public string Code { get; set; } = "";
        public string Winery { get; set; } = "";
        public string Name { get; set; } = "";       // cellar label if typed, else the code
        public string Type { get; set; } = "tank";
        public bool Fermenting { get; set; }
        public double Temp { get; set; }
        public double BaseTemp { get; set; }
        public double Level { get; set; }
        public double BaseLevel { get; set; }
        public double? Capacity { get; set; }
        public double? Density { get; set; }
        public double? BaseDensity { get; set; }
        public bool Streaming { get; set; } = true; // F-1 only streams during "Missing data"
        public string Status { get; set; } = "ok";  // ok | amber | red
        public List<(DateTime t, double v)> LevelHist { get; } = [];
        public List<(DateTime t, double v)> DensHist { get; } = [];
        public Dictionary<string, string> Alerts { get; } = []; // rule -> level
        // Range alerts (config/ranges.json)
        public string? Batch { get; set; }
        public string WineColor { get; set; } = "red";
        public Dictionary<string, RangeRule> Ranges { get; } = [];
        public RangeTracker Range { get; } = new();
    }

    /// Measurements checked against config/ranges.json (the simulator streams these three).
    public static readonly string[] RangeMetrics = ["temperature", "density", "level"];

    public bool On { get; set; }
    public string Scenario { get; private set; } = "allgood";
    public DateTime SimTime { get; private set; }
    /// Real time Live was switched on: only operations recorded after this count as "an operation was recorded".
    public DateTime StartedUtc { get; private set; }
    public List<VState> Vessels { get; } = [];
    readonly Random rnd = seed == null ? new Random() : new Random(seed.Value);
    int step;
    readonly List<LiveEvent> pendingNotes = [];
    IReadOnlyDictionary<string, string> settings = SettingDefaults.Copy();

    double Noise(double amp) => (rnd.NextDouble() * 2 - 1) * amp;
    double S(string k) => settings.Number(k);

    /// Active demo vessels: F-2 (fermenting), Q-3 (qvevri), T1-2 (storage); F-1 joins for "Missing data".
    static readonly (string Winery, string Code, bool Fermenting, bool Streaming)[] Active =
        [("W01", "F-2", true, true), ("W03", "Q-3", false, true), ("W01", "T1-2", false, true), ("W01", "F-1", true, false)];

    /// Base values start from the vessel's last recorded value; when there is none, a simulated default (all of it is SIMULATED).
    public async Task StartAsync(IReadOnlyDictionary<string, string>? s = null)
    {
        settings = s ?? await settingsRepository.GetAsync();
        Vessels.Clear(); pendingNotes.Clear();
        SimTime = new DateTime(2026, 10, 1, 8, 0, 0);
        StartedUtc = DateTime.UtcNow;
        step = 0; Scenario = "allgood";
        var contents = new Dictionary<long, double>();
        foreach (var b in await batches.ListRealIdsAsync())
            foreach (var (code, l) in (await losses.AnalyzeAsync(b, settings, compare: false)).VesselLitres)
                foreach (var v in await vessels.ListIdsAsync(b, code))
                    contents[v] = contents.GetValueOrDefault(v) + l;
        foreach (var (w, code, ferm, streaming) in Active)
        {
            var v = await vessels.GetAsync(w, code);
            if (v == null) continue;
            long id = v.Id;
            double? cap = v.CapacityL;
            double? lastT = await readings.LastValueAsync(id, "temperature");
            double? lastD = await readings.LastValueAsync(id, "hydrometer_density");
            double litres = contents.GetValueOrDefault(id);
            double baseLevel = litres > 0 ? litres : Math.Round((cap ?? 1000) * 0.7);
            double baseT = lastT ?? (ferm ? 25.5 : 14.0);
            var label = v.CellarLabel;
            Vessels.Add(new VState
            {
                Id = id, Code = code, Winery = w, Name = string.IsNullOrWhiteSpace(label) ? code : label!, Type = v.Type, Fermenting = ferm, Streaming = streaming,
                Temp = baseT, BaseTemp = baseT, Level = baseLevel, BaseLevel = baseLevel, Capacity = cap,
                Density = ferm ? lastD ?? 1.060 : null, BaseDensity = ferm ? lastD ?? 1.060 : null,
            });
            Vessels[^1].LevelHist.Add((SimTime, baseLevel));
            // batch now (or last) in the vessel -> wine type -> which default range applies
            var b = await vessels.LastRealBatchAsync(id);
            var wineType = b?.WineType ?? await vessels.FirstRealWineTypeAsync(w);
            var vs = Vessels[^1];
            vs.Batch = b?.Code;
            vs.WineColor = RangeConfig.WineColorOf(wineType);
            foreach (var m in RangeMetrics)
                if (rangeConfig.For(w, code, m, ferm, vs.WineColor, cap) is { } r) vs.Ranges[m] = r;
        }
    }

    /// Settings changed (thresholds are editable): the running simulator uses them from the next tick.
    public void UseSettings(IReadOnlyDictionary<string, string> s) => settings = s;

    public double? AllowedMax(string vesselCode, string metric) => V(vesselCode)?.Ranges.GetValueOrDefault(metric)?.Max;

    VState? V(string code) => Vessels.FirstOrDefault(v => v.Code == code);

    /// Missing-data target: F-2 if it has no Initial Brix recorded, otherwise F-1 (the dataset has no Brix for it).
    async Task<string> MissingTargetAsync() => await readings.VesselHasInitialBrixAsync("F-2") ? "F-1" : "F-2";

    public async Task SetScenarioAsync(string scenario)
    {
        if (!LiveScenarios.All.Contains(scenario)) throw new ArgumentException("unknown scenario");
        Scenario = scenario; step = 0;
        foreach (var v in Vessels) { v.Streaming = v.Code != "F-1"; }
        var f2 = V("F-2");
        switch (scenario)
        {
            case "hot": if (f2 != null) f2.Temp = 27.0; break;
            case "stuck":
                if (f2 != null) { if ((f2.Density ?? 0) <= 1.015) f2.Density = 1.045; f2.DensHist.Clear(); }
                break;
            case "missing":
                var target = V(await MissingTargetAsync());
                if (target != null) { target.Streaming = true; target.Density ??= 1.050; }
                break;
            case "allgood":
                foreach (var v in Vessels)
                {
                    v.Temp = v.BaseTemp; v.Level = v.BaseLevel; v.LevelHist.Clear(); v.DensHist.Clear();
                    if (v.Fermenting) v.Density = v.BaseDensity;
                    var had = v.Alerts.Count > 0;
                    v.Alerts.Clear(); v.Range.Reset();
                    if (had) pendingNotes.Add(Note(v, "green", "back_normal", $"{v.Name} back to normal."));
                    v.Status = "ok";
                }
                if (pendingNotes.Count == 0) pendingNotes.Add(Note(null, "green", "all_good", "All vessels in range."));
                break;
        }
    }

    LiveEvent Note(VState? v, string level, string rule, string msg, double? usd = null) => new()
    {
        Kind = "notification", Level = level, Rule = rule, Message = msg, ValueUsd = usd, VesselId = v?.Id, VesselCode = v?.Code, VesselName = v?.Name,
        Scenario = Scenario,
    };

    LiveEvent Reading(VState v, string metric, double value, string unit) => new()
    {
        Kind = "reading", VesselId = v.Id, VesselCode = v.Code, VesselName = v.Name, Metric = metric, Value = value, Unit = unit, Scenario = Scenario,
    };

    /// One simulator step (every 3 s when Live is ON). Events are written to live_events in the given transaction.
    public async Task<List<LiveEvent>> TickAsync()
    {
        var stepLen = Scenario == "stuck" ? TimeSpan.FromHours(6) : TimeSpan.FromMinutes(15);
        SimTime += stepLen; step++;
        var evs = new List<LiveEvent>(pendingNotes); pendingNotes.Clear();
        foreach (var v in Vessels.Where(v => v.Streaming))
        {
            // temperature
            if (Scenario == "hot" && v.Code == "F-2") v.Temp = Math.Min(32.0, v.Temp + 0.5);
            else v.Temp += (v.BaseTemp - v.Temp) * 0.35;
            double temp = Math.Round(v.Temp + Noise(0.3), 1);
            // level (litres from a simulated dip)
            if (Scenario == "loss" && v.Code == "Q-3" && step <= 3) v.Level -= 25.0 / 3;
            double level = Math.Round(v.Level + Noise(2), 1);
            v.LevelHist.Add((SimTime, level));
            evs.Add(Reading(v, "temperature", temp, "°C"));
            evs.Add(Reading(v, "level", level, "L"));
            double? dens = null;
            // density trending down while fermenting (flat when stuck)
            if (v.Fermenting && v.Density != null)
            {
                bool stuck = Scenario == "stuck" && v.Code == "F-2";
                if (!stuck && v.Density > 0.996) v.Density = Math.Max(0.996, v.Density.Value - 0.0006 * stepLen.TotalHours);
                double d = Math.Round(v.Density.Value + (stuck ? 0 : Noise(0.0002)), 4);
                v.DensHist.Add((SimTime, d));
                evs.Add(Reading(v, "density", d, "SG"));
                dens = d;
            }
            evs.AddRange(await RulesAsync(v, new() { ["temperature"] = temp, ["level"] = level, ["density"] = dens }));
            v.Status = v.Alerts.Values.Contains("red") ? "red" : v.Alerts.Count > 0 ? "amber" : "ok";
            // keep only what the rules need
            v.LevelHist.RemoveAll(h => h.t < SimTime - TimeSpan.FromHours(6));
            v.DensHist.RemoveAll(h => h.t < SimTime - TimeSpan.FromHours(S("live_stuck_hours") + 12));
        }
        var now = DateTime.UtcNow.ToString("o");
        var sim = SimTime.ToString("yyyy-MM-ddTHH:mm");
        foreach (var e in evs)
        {
            e.CreatedAt = now; e.SimTime = sim;
            e.Id = await events.InsertAsync(e);
        }
        return evs;
    }

    /// Rules → notifications. Thresholds are DEMO ASSUMPTIONS (Settings). A notification fires on entering a state, green on leaving it.
    async Task<List<LiveEvent>> RulesAsync(VState v, Dictionary<string, double?> values)
    {
        var outp = new List<LiveEvent>();
        void Set(string rule, bool active, string level, Func<string> msg, double? usd = null)
        {
            bool was = v.Alerts.ContainsKey(rule);
            if (active && !was) { v.Alerts[rule] = level; outp.Add(Note(v, level, rule, msg(), usd)); }
            else if (!active && was)
            {
                v.Alerts.Remove(rule);
                if (v.Alerts.Count == 0) outp.Add(Note(v, "green", "back_normal", $"{v.Name} back to normal."));
            }
        }
        // Range alerts (config/ranges.json): only these new readings are checked; 3 out of range in a row -> one red alert, cleared when back in range.
        foreach (var m in RangeMetrics)
        {
            var r = v.Ranges.GetValueOrDefault(m);
            var (change, dir) = v.Range.Check(m, values.GetValueOrDefault(m), r, v.Capacity, SimTime.ToString("yyyy-MM-ddTHH:mm"));
            if (change == RangeTracker.Change.Fire)
            {
                var rule = RangeRuleName(m, dir);
                v.Alerts[rule] = "red";
                var x = values[m]!.Value;
                outp.Add(Note(v, "red", rule, $"{v.Name}{(v.Batch != null ? $" ({v.Batch})" : "")}: {m} {dir} — {RangeRule.Format(x)} {r!.Unit} " +
                    $"(allowed {RangeRule.Format(r.Min)}–{RangeRule.Format(r.Max)} {r.Unit}, {RangeTracker.InARow} readings in a row)."));
            }
            else if (change == RangeTracker.Change.Clear)
            {
                v.Alerts.Remove(RangeRuleName(m, "too high")); v.Alerts.Remove(RangeRuleName(m, "too low"));
                if (v.Alerts.Count == 0) outp.Add(Note(v, "green", "back_normal", $"{v.Name} back to normal."));
            }
        }

        // level drop within the window with no operation recorded on that vessel in the last hour
        var window = TimeSpan.FromHours(S("live_level_window_h"));
        var recent = v.LevelHist.Where(h => h.t >= SimTime - window).ToList();
        double drop = recent.Count > 0 ? recent.Max(h => h.v) - recent[^1].v : 0;
        if (drop > S("live_level_drop_l") && !v.Alerts.ContainsKey("level_drop"))
        {
            bool opRecorded = await operations.AnyRecordedSinceAsync(v.Id, (StartedUtc > DateTime.UtcNow.AddHours(-1) ? StartedUtc : DateTime.UtcNow.AddHours(-1)).ToString("o"));
            if (!opRecorded)
            {
                double lost = Math.Round(drop);
                double usd = Math.Round(lost * S("value_per_litre_usd"), 2);
                Set("level_drop", true, "red", () => $"{v.Name} lost ≈{lost:0} L with no operation recorded. Check for a leak or an unrecorded transfer.", usd);
            }
        }
        // a level alert stays until "All good" (the wine is gone; only a person can close it)

        if (v.Fermenting && v.Density != null && v.DensHist.Count > 1)
        {
            double hours = S("live_stuck_hours");
            var anchor = v.DensHist.LastOrDefault(h => h.t <= SimTime - TimeSpan.FromHours(hours));
            bool full = anchor != default;
            var span = v.DensHist.Where(h => h.t >= (full ? anchor.t : DateTime.MinValue)).Select(h => h.v).ToList();
            bool flat = full && span.Max() - span.Min() < S("live_stuck_delta_sg") && v.Density > S("live_stuck_min_sg");
            Set("stuck", flat, "amber", () => $"{v.Name} fermentation may be stuck: density flat at {v.Density.Value.ToString("0.000", CultureInfo.InvariantCulture)} for {hours:0} h.");
        }

        if (Scenario == "missing" && v.Fermenting && v.Code == await MissingTargetAsync())
            Set("missing_brix", true, "amber", () => $"{v.Name} has no Initial Brix — potential alcohol can't be calculated.");
        return outp;
    }

    /// Rule names: temperature keeps "temp_high" (as before) plus "temp_low"; the others are e.g. "density_low", "level_high".
    static string RangeRuleName(string metric, string dir) => (metric == "temperature" ? "temp" : metric) + (dir == "too low" ? "_low" : "_high");

    public LiveStateDto State() => new(
        On, Scenario, LiveScenarios.Titles[Scenario], SimTime.ToString("yyyy-MM-ddTHH:mm"),
        new LiveThresholdsDto(V("F-2")?.Ranges.GetValueOrDefault("temperature")?.Max ?? 30, S("live_level_drop_l"), S("live_stuck_min_sg")),
        Vessels.SelectMany(v => v.Range.Alerts.Values.Select(a =>
            new RangeAlertDto(v.Code, v.Name, v.Batch, a.Metric, a.Direction, a.Value, a.Range.Min, a.Range.Max, a.Range.Unit, a.Since, "SIMULATED"))).ToList(),
        Vessels.Where(v => v.Streaming).Select(v => new LiveVesselDto(
            v.Id, v.Code, v.Winery, v.Name, v.Type, v.Fermenting, Math.Round(v.Temp, 1), Math.Round(v.Level, 1), v.Capacity,
            v.Density == null ? null : Math.Round(v.Density.Value, 4), v.Status, new Dictionary<string, string>(v.Alerts), v.Batch, v.WineColor,
            v.Ranges.ToDictionary(kv => kv.Key, kv => new RangeStateDto(kv.Value.Min, kv.Value.Max, kv.Value.Unit, kv.Value.From,
                v.Range.InRange.TryGetValue(kv.Key, out var ok) ? ok : null)))).ToList(),
        "SIMULATED");
}
