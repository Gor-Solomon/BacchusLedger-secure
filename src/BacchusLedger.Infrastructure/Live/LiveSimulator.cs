using System.Globalization;
using System.Text.Json.Nodes;
using BacchusLedger.Application.Contracts.Live;
using BacchusLedger.Application.Events;
using BacchusLedger.Domain.Dataset;
using BacchusLedger.Application.Extensions;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Application.Services;
using BacchusLedger.Domain.Ranges;

namespace BacchusLedger.Infrastructure.Live;

/// LIVE mode: everything here is SIMULATED, stored only in live_events, and never read by journals, the loss engine or ROI numbers.
/// The simulator keeps its state between ticks; its data access (deps) belongs to one DbContext scope and is replaced with Use(…)
/// by the background host before every step.
public sealed class LiveSimulator(LiveSimulatorDependencies? deps, RangeConfig rangeConfig, int? seed) : ILiveSimulator
{
    LiveSimulatorDependencies Deps => deps ?? throw new InvalidOperationException("live simulator used outside a scope");
    IBatchRepository batches => Deps.Batches;
    IVesselRepository vessels => Deps.Vessels;
    IReadingRepository readings => Deps.Readings;
    IOperationRepository operations => Deps.Operations;
    ILiveEventRepository events => Deps.Events;
    ISettingsRepository settingsRepository => Deps.Settings;
    ILossAnalysisService losses => Deps.Losses;

    /// Binds the simulator to the services of the current scope.
    public void Use(LiveSimulatorDependencies scoped) => deps = scoped;

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
    /// Guards the vessel list and the rule state (alerts, range trackers, histories): the rules run on the outbox worker's thread.
    readonly object rules = new();
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
        var simStart = new DateTime(2026, 10, 1, 8, 0, 0);
        var fresh = new List<VState>();
        var contents = new Dictionary<long, double>();
        var real = await batches.ListRealIdsAsync();
        var results = await losses.AnalyzeManyAsync(real, settings);
        foreach (var b in real)
            foreach (var (code, l) in results[b].VesselLitres)
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
            var vs = new VState
            {
                Id = id, Code = code, Winery = w, Name = string.IsNullOrWhiteSpace(label) ? code : label!, Type = v.Type, Fermenting = ferm, Streaming = streaming,
                Temp = baseT, BaseTemp = baseT, Level = baseLevel, BaseLevel = baseLevel, Capacity = cap,
                Density = ferm ? lastD ?? 1.060 : null, BaseDensity = ferm ? lastD ?? 1.060 : null,
            };
            vs.LevelHist.Add((simStart, baseLevel));
            // batch now (or last) in the vessel -> wine type -> which default range applies
            var b = await vessels.LastRealBatchAsync(id);
            var wineType = b?.WineType ?? await vessels.FirstRealWineTypeAsync(w);
            vs.Batch = b?.Code;
            vs.WineColor = RangeConfig.WineColorOf(wineType);
            foreach (var m in RangeMetrics)
                if (rangeConfig.For(w, code, m, ferm, vs.WineColor, cap) is { } r) vs.Ranges[m] = r;
            fresh.Add(vs);
        }
        lock (rules)
        {
            Vessels.Clear(); Vessels.AddRange(fresh); pendingNotes.Clear();
            SimTime = simStart;
            StartedUtc = DateTime.UtcNow;
            step = 0; Scenario = "allgood";
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
        var missingTarget = scenario == "missing" ? await MissingTargetAsync() : null;
        lock (rules) Switch(scenario, missingTarget);
    }

    void Switch(string scenario, string? missingTarget)
    {
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
                var target = V(missingTarget!);
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

    LiveEvent Note(VState? v, string level, string rule, string msg, double? usd = null, string? scenario = null) => new()
    {
        Kind = "notification", Level = level, Rule = rule, Message = msg, ValueUsd = usd, VesselId = v?.Id, VesselCode = v?.Code, VesselName = v?.Name,
        Scenario = scenario ?? this.Scenario,
    };

    LiveEvent Reading(VState v, string metric, double value, string unit) => new()
    {
        Kind = "reading", VesselId = v.Id, VesselCode = v.Code, VesselName = v.Name, Metric = metric, Value = value, Unit = unit, Scenario = Scenario,
    };

    /// One simulator step (every 3 s when Live is ON), without the rules. Readings and waiting notifications are written to
    /// live_events in the given transaction; each vessel's readings come back as a LiveReadingSimulated event for the rules.
    public async Task<SimulatedTick> ProduceAsync()
    {
        var stepLen = Scenario == "stuck" ? TimeSpan.FromHours(6) : TimeSpan.FromMinutes(15);
        SimTime += stepLen; step++;
        var now = DateTime.UtcNow;
        var created = now.ToString("o");
        var sim = SimTime.ToString("yyyy-MM-ddTHH:mm");
        List<LiveEvent> notes;
        lock (rules) { notes = [.. pendingNotes]; pendingNotes.Clear(); }
        foreach (var e in notes)
        {
            e.CreatedAt = created; e.SimTime = sim;
            e.Id = await events.InsertAsync(e);
        }
        // facts the rules need from the database, taken now: Missing-data target, and operations recorded since Live started (at most 1 h)
        var missingTarget = Scenario == "missing" ? await MissingTargetAsync() : null;
        var opsSince = (StartedUtc > now.AddHours(-1) ? StartedUtc : now.AddHours(-1)).ToString("o");
        var produced = new List<LiveReadingSimulated>();
        foreach (var v in Vessels.Where(v => v.Streaming).ToList())
        {
            // temperature
            if (Scenario == "hot" && v.Code == "F-2") v.Temp = Math.Min(32.0, v.Temp + 0.5);
            else v.Temp += (v.BaseTemp - v.Temp) * 0.35;
            double temp = Math.Round(v.Temp + Noise(0.3), 1);
            // level (litres from a simulated dip)
            if (Scenario == "loss" && v.Code == "Q-3" && step <= 3) v.Level -= 25.0 / 3;
            double level = Math.Round(v.Level + Noise(2), 1);
            var readings = new List<LiveEvent> { Reading(v, "temperature", temp, "°C"), Reading(v, "level", level, "L") };
            // density trending down while fermenting (flat when stuck)
            if (v.Fermenting && v.Density != null)
            {
                bool stuck = Scenario == "stuck" && v.Code == "F-2";
                if (!stuck && v.Density > 0.996) v.Density = Math.Max(0.996, v.Density.Value - 0.0006 * stepLen.TotalHours);
                readings.Add(Reading(v, "density", Math.Round(v.Density.Value + (stuck ? 0 : Noise(0.0002)), 4), "SG"));
            }
            foreach (var r in readings)
            {
                r.CreatedAt = created; r.SimTime = sim;
                r.Id = await events.InsertAsync(r);
            }
            var opRecorded = await operations.AnyRecordedSinceAsync(v.Id, opsSince);
            produced.Add(new LiveReadingSimulated(Guid.NewGuid(), now, v.Code,
                new SimulatedReadings(SimTime, Scenario, readings, opRecorded, v.Code == missingTarget)));
        }
        return new SimulatedTick(notes, produced);
    }

    /// The rules on one vessel's readings (RangeRulesHandler, or TickAsync). In memory only; times and scenario come from the readings.
    public IReadOnlyList<LiveEvent> ApplyRules(LiveReadingSimulated e)
    {
        var p = e.Payload;
        double? Value(string metric) => p.Readings.FirstOrDefault(r => r.Metric == metric)?.Value;
        lock (rules)
        {
            if (V(e.Vessel ?? "") is not { } v) return [];
            double? level = Value("level"), dens = Value("density");
            if (level != null) v.LevelHist.Add((p.SimTime, level.Value));
            if (dens != null) v.DensHist.Add((p.SimTime, dens.Value));
            var notes = Rules(v, new() { ["temperature"] = Value("temperature"), ["level"] = level, ["density"] = dens }, p);
            v.Status = v.Alerts.Values.Contains("red") ? "red" : v.Alerts.Count > 0 ? "amber" : "ok";
            // keep only what the rules need
            v.LevelHist.RemoveAll(h => h.t < p.SimTime - TimeSpan.FromHours(6));
            v.DensHist.RemoveAll(h => h.t < p.SimTime - TimeSpan.FromHours(S("live_stuck_hours") + 12));
            var created = p.Readings.FirstOrDefault()?.CreatedAt ?? e.OccurredAt.ToString("o");
            foreach (var n in notes) { n.CreatedAt = created; n.SimTime = p.SimTime.ToString("yyyy-MM-ddTHH:mm"); }
            return notes;
        }
    }

    /// One step with the rules run at once and their notifications stored too (the selftest's simulator).
    public async Task<List<LiveEvent>> TickAsync()
    {
        var tick = await ProduceAsync();
        var evs = new List<LiveEvent>(tick.Notes);
        foreach (var r in tick.Readings)
        {
            evs.AddRange(r.Payload.Readings);
            foreach (var n in ApplyRules(r))
            {
                n.Id = await events.InsertAsync(n);
                evs.Add(n);
            }
        }
        return evs;
    }

    /// Rules → notifications. Thresholds are DEMO ASSUMPTIONS (Settings). A notification fires on entering a state, green on leaving it.
    List<LiveEvent> Rules(VState v, Dictionary<string, double?> values, SimulatedReadings p)
    {
        var outp = new List<LiveEvent>();
        var at = p.SimTime;
        void Set(string rule, bool active, string level, Func<string> msg, double? usd = null)
        {
            bool was = v.Alerts.ContainsKey(rule);
            if (active && !was) { v.Alerts[rule] = level; outp.Add(Note(v, level, rule, msg(), usd, p.Scenario)); }
            else if (!active && was)
            {
                v.Alerts.Remove(rule);
                if (v.Alerts.Count == 0) outp.Add(Note(v, "green", "back_normal", $"{v.Name} back to normal.", scenario: p.Scenario));
            }
        }
        // Range alerts (config/ranges.json): only these new readings are checked; 3 out of range in a row -> one red alert, cleared when back in range.
        foreach (var m in RangeMetrics)
        {
            var r = v.Ranges.GetValueOrDefault(m);
            var (change, dir) = v.Range.Check(m, values.GetValueOrDefault(m), r, v.Capacity, at.ToString("yyyy-MM-ddTHH:mm"));
            if (change == RangeTracker.Change.Fire)
            {
                var rule = RangeRuleName(m, dir);
                v.Alerts[rule] = "red";
                var x = values[m]!.Value;
                outp.Add(Note(v, "red", rule, $"{v.Name}{(v.Batch != null ? $" ({v.Batch})" : "")}: {m} {dir} — {RangeRule.Format(x)} {r!.Unit} " +
                    $"(allowed {RangeRule.Format(r.Min)}–{RangeRule.Format(r.Max)} {r.Unit}, {RangeTracker.InARow} readings in a row).", scenario: p.Scenario));
            }
            else if (change == RangeTracker.Change.Clear)
            {
                v.Alerts.Remove(RangeRuleName(m, "too high")); v.Alerts.Remove(RangeRuleName(m, "too low"));
                if (v.Alerts.Count == 0) outp.Add(Note(v, "green", "back_normal", $"{v.Name} back to normal.", scenario: p.Scenario));
            }
        }

        // level drop within the window with no operation recorded on that vessel in the last hour
        var window = TimeSpan.FromHours(S("live_level_window_h"));
        var recent = v.LevelHist.Where(h => h.t >= at - window).ToList();
        double drop = recent.Count > 0 ? recent.Max(h => h.v) - recent[^1].v : 0;
        if (drop > S("live_level_drop_l") && !v.Alerts.ContainsKey("level_drop") && !p.OperationRecorded)
        {
            double lost = Math.Round(drop);
            double usd = Math.Round(lost * S("value_per_litre_usd"), 2);
            Set("level_drop", true, "red", () => $"{v.Name} lost ≈{lost:0} L with no operation recorded. Check for a leak or an unrecorded transfer.", usd);
        }
        // a level alert stays until "All good" (the wine is gone; only a person can close it)

        if (v.Fermenting && v.Density != null && v.DensHist.Count > 1)
        {
            double hours = S("live_stuck_hours");
            var anchor = v.DensHist.LastOrDefault(h => h.t <= at - TimeSpan.FromHours(hours));
            bool full = anchor != default;
            var span = v.DensHist.Where(h => h.t >= (full ? anchor.t : DateTime.MinValue)).Select(h => h.v).ToList();
            bool flat = full && span.Max() - span.Min() < S("live_stuck_delta_sg") && v.Density > S("live_stuck_min_sg");
            Set("stuck", flat, "amber", () => $"{v.Name} fermentation may be stuck: density flat at {v.Density.Value.ToString("0.000", CultureInfo.InvariantCulture)} for {hours:0} h.");
        }

        if (p.Scenario == "missing" && v.Fermenting && p.MissingBrixTarget)
            Set("missing_brix", true, "amber", () => $"{v.Name} has no Initial Brix — potential alcohol can't be calculated.");
        return outp;
    }

    /// Rule names: temperature keeps "temp_high" (as before) plus "temp_low"; the others are e.g. "density_low", "level_high".
    static string RangeRuleName(string metric, string dir) => (metric == "temperature" ? "temp" : metric) + (dir == "too low" ? "_low" : "_high");

    public LiveStateDto State()
    {
        lock (rules) return BuildState();
    }

    LiveStateDto BuildState() => new(
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
