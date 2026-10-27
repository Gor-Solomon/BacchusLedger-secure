using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;

namespace BacchusLedger;

/// Update 3: LIVE mode. Everything here is SIMULATED, stored only in `live_events`, and never read by
/// journals, the loss engine or ROI numbers.
public sealed class LiveEvent
{
    public long Id { get; set; }
    public string CreatedAt { get; set; } = "";
    public string SimTime { get; set; } = "";
    public long? VesselId { get; set; }
    public string? VesselCode { get; set; }
    public string? VesselName { get; set; }
    public string Kind { get; set; } = "reading"; // reading | notification
    public string? Metric { get; set; }           // temperature | level | density
    public double? Value { get; set; }
    public string? Unit { get; set; }
    public string? Level { get; set; }            // red | amber | green (notifications)
    public string? Rule { get; set; }             // temp_high | level_drop | stuck | missing_brix | back_normal | all_good
    public string? Message { get; set; }
    public double? ValueUsd { get; set; }
    public string? Scenario { get; set; }
    public string Source { get; set; } = "SIMULATED";
    public bool Acknowledged { get; set; }
}

public sealed class LiveSim
{
    public static readonly string[] Scenarios = ["hot", "loss", "stuck", "missing", "allgood"];
    public static readonly Dictionary<string, string> ScenarioTitles = new()
    {
        ["hot"] = "Hot fermentation", ["loss"] = "Unrecorded wine loss", ["stuck"] = "Stuck fermentation", ["missing"] = "Missing data", ["allgood"] = "All good",
    };

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
        public Dictionary<string, Ranges.Range> Ranges { get; } = [];
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
    readonly Random rnd;
    int step;
    readonly List<LiveEvent> pendingNotes = [];
    Dictionary<string, string> settings = new(Db.DefaultSettings);

    public LiveSim(int? seed = null) { rnd = seed == null ? new Random() : new Random(seed.Value); }

    /// Range config (config/ranges.json); the selftest can pass its own.
    public System.Text.Json.Nodes.JsonNode? RangeConfig { get; init; } = Ranges.Config;

    double Noise(double amp) => (rnd.NextDouble() * 2 - 1) * amp;
    double S(string k) => Db.SettingD(settings, k);

    /// Active demo vessels: F-2 (fermenting), Q-3 (qvevri), T1-2 (storage); F-1 joins for "Missing data".
    static readonly (string Winery, string Code, bool Fermenting, bool Streaming)[] Active =
        [("W01", "F-2", true, true), ("W03", "Q-3", false, true), ("W01", "T1-2", false, true), ("W01", "F-1", true, false)];

    /// Base values start from the vessel's last recorded value; when there is none, a simulated default (all of it is SIMULATED).
    public void Start(SqliteConnection c, Dictionary<string, string>? s = null)
    {
        settings = s ?? Db.Settings();
        Vessels.Clear(); pendingNotes.Clear();
        SimTime = new DateTime(2026, 10, 1, 8, 0, 0);
        StartedUtc = DateTime.UtcNow;
        step = 0; Scenario = "allgood";
        var contents = new Dictionary<long, double>();
        foreach (var b in Db.Query(c, "SELECT id FROM batches WHERE COALESCE(source_note,'') NOT LIKE '%copy%'"))
            foreach (var (code, l) in LossEngine.Analyze(c, (long)b["id"]!, settings, compare: false).VesselLitres)
                foreach (var v in Db.Query(c, "SELECT v.id FROM vessels v JOIN batches b ON b.winery_id=v.winery_id WHERE b.id=$1 AND v.code=$2", b["id"], code))
                    contents[(long)v["id"]!] = contents.GetValueOrDefault((long)v["id"]!) + l;
        foreach (var (w, code, ferm, streaming) in Active)
        {
            var v = Db.Query(c, "SELECT v.* FROM vessels v JOIN wineries w ON w.id=v.winery_id WHERE w.code=$1 AND v.code=$2", w, code).FirstOrDefault();
            if (v == null) continue;
            long id = (long)v["id"]!;
            double? cap = Db.D(v["capacity_l"]);
            double? lastT = Db.D(Db.Query(c, "SELECT value FROM readings WHERE vessel_id=$1 AND type='temperature' ORDER BY taken_at DESC, id DESC LIMIT 1", id).FirstOrDefault()?["value"]);
            double? lastD = Db.D(Db.Query(c, "SELECT value FROM readings WHERE vessel_id=$1 AND type='hydrometer_density' ORDER BY taken_at DESC, id DESC LIMIT 1", id).FirstOrDefault()?["value"]);
            double litres = contents.GetValueOrDefault(id);
            double baseLevel = litres > 0 ? litres : Math.Round((cap ?? 1000) * 0.7);
            double baseT = lastT ?? (ferm ? 25.5 : 14.0);
            var label = v["cellar_label"] as string;
            Vessels.Add(new VState
            {
                Id = id, Code = code, Winery = w, Name = string.IsNullOrWhiteSpace(label) ? code : label!, Type = (string)v["type"]!, Fermenting = ferm, Streaming = streaming,
                Temp = baseT, BaseTemp = baseT, Level = baseLevel, BaseLevel = baseLevel, Capacity = cap,
                Density = ferm ? lastD ?? 1.060 : null, BaseDensity = ferm ? lastD ?? 1.060 : null,
            });
            Vessels[^1].LevelHist.Add((SimTime, baseLevel));
            // batch now (or last) in the vessel -> wine type -> which default range applies
            var b = Db.Query(c, """
                SELECT b.code, b.wine_type FROM batches b JOIN (
                  SELECT batch_id, date AS d FROM operations WHERE from_vessel_id=$1 OR to_vessel_id=$1
                  UNION ALL SELECT batch_id, taken_at FROM readings WHERE vessel_id=$1
                  UNION ALL SELECT batch_id, date FROM lab WHERE vessel_id=$1) x ON x.batch_id=b.id
                WHERE COALESCE(b.source_note,'') NOT LIKE '%copy%' GROUP BY b.id ORDER BY MAX(x.d) DESC LIMIT 1
                """, id).FirstOrDefault();
            var wineType = b?["wine_type"] as string ?? Db.Query(c, """
                SELECT b.wine_type FROM batches b JOIN wineries w ON w.id=b.winery_id WHERE w.code=$1 AND COALESCE(b.source_note,'') NOT LIKE '%copy%' ORDER BY b.code LIMIT 1
                """, w).FirstOrDefault()?["wine_type"] as string;
            var vs = Vessels[^1];
            vs.Batch = b?["code"] as string;
            vs.WineColor = Ranges.WineColor(wineType);
            foreach (var m in RangeMetrics)
                if (Ranges.For(RangeConfig, w, code, m, ferm, vs.WineColor, cap) is { } r) vs.Ranges[m] = r;
        }
    }

    /// Settings changed (thresholds are editable): the running simulator uses them from the next tick.
    public void UseSettings(Dictionary<string, string> s) => settings = s;

    VState? V(string code) => Vessels.FirstOrDefault(v => v.Code == code);

    /// Missing-data target: F-2 if it has no Initial Brix recorded, otherwise F-1 (the dataset has no Brix for it).
    string MissingTarget(SqliteConnection c) =>
        Db.Query(c, "SELECT 1 FROM readings r JOIN vessels v ON v.id=r.vessel_id WHERE v.code='F-2' AND r.type='initial_brix'").Count == 0 ? "F-2" : "F-1";

    public void SetScenario(SqliteConnection c, string scenario)
    {
        if (!Scenarios.Contains(scenario)) throw new ArgumentException("unknown scenario");
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
                var target = V(MissingTarget(c));
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
    public List<LiveEvent> Tick(SqliteConnection c, SqliteTransaction tx)
    {
        var stepLen = Scenario == "stuck" ? TimeSpan.FromHours(6) : TimeSpan.FromMinutes(15);
        SimTime += stepLen; step++;
        var events = new List<LiveEvent>(pendingNotes); pendingNotes.Clear();
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
            events.Add(Reading(v, "temperature", temp, "°C"));
            events.Add(Reading(v, "level", level, "L"));
            double? dens = null;
            // density trending down while fermenting (flat when stuck)
            if (v.Fermenting && v.Density != null)
            {
                bool stuck = Scenario == "stuck" && v.Code == "F-2";
                if (!stuck && v.Density > 0.996) v.Density = Math.Max(0.996, v.Density.Value - 0.0006 * stepLen.TotalHours);
                double d = Math.Round(v.Density.Value + (stuck ? 0 : Noise(0.0002)), 4);
                v.DensHist.Add((SimTime, d));
                events.Add(Reading(v, "density", d, "SG"));
                dens = d;
            }
            events.AddRange(Rules(c, v, new() { ["temperature"] = temp, ["level"] = level, ["density"] = dens }));
            v.Status = v.Alerts.Values.Contains("red") ? "red" : v.Alerts.Count > 0 ? "amber" : "ok";
            // keep only what the rules need
            v.LevelHist.RemoveAll(h => h.t < SimTime - TimeSpan.FromHours(6));
            v.DensHist.RemoveAll(h => h.t < SimTime - TimeSpan.FromHours(S("live_stuck_hours") + 12));
        }
        var now = DateTime.UtcNow.ToString("o");
        var sim = SimTime.ToString("yyyy-MM-ddTHH:mm");
        foreach (var e in events)
        {
            e.CreatedAt = now; e.SimTime = sim;
            e.Id = Db.Insert(c, tx, """
                INSERT INTO live_events(created_at,sim_time,vessel_id,vessel_code,vessel_name,kind,metric,value,unit,level,rule,message,value_usd,scenario,source,acknowledged)
                VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,0)
                """, e.CreatedAt, e.SimTime, e.VesselId, e.VesselCode, e.VesselName, e.Kind, e.Metric, e.Value, e.Unit, e.Level, e.Rule, e.Message, e.ValueUsd, e.Scenario, e.Source);
        }
        return events;
    }

    /// Rules → notifications. Thresholds are DEMO ASSUMPTIONS (Settings). A notification fires on entering a state, green on leaving it.
    IEnumerable<LiveEvent> Rules(SqliteConnection c, VState v, Dictionary<string, double?> values)
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
                var rule = RangeRule(m, dir);
                v.Alerts[rule] = "red";
                var x = values[m]!.Value;
                outp.Add(Note(v, "red", rule, $"{v.Name}{(v.Batch != null ? $" ({v.Batch})" : "")}: {m} {dir} — {Ranges.Fmt(x)} {r!.Unit} " +
                    $"(allowed {Ranges.Fmt(r.Min)}–{Ranges.Fmt(r.Max)} {r.Unit}, {RangeTracker.InARow} readings in a row)."));
            }
            else if (change == RangeTracker.Change.Clear)
            {
                v.Alerts.Remove(RangeRule(m, "too high")); v.Alerts.Remove(RangeRule(m, "too low"));
                if (v.Alerts.Count == 0) outp.Add(Note(v, "green", "back_normal", $"{v.Name} back to normal."));
            }
        }

        // level drop within the window with no operation recorded on that vessel in the last hour
        var window = TimeSpan.FromHours(S("live_level_window_h"));
        var recent = v.LevelHist.Where(h => h.t >= SimTime - window).ToList();
        double drop = recent.Count > 0 ? recent.Max(h => h.v) - recent[^1].v : 0;
        if (drop > S("live_level_drop_l") && !v.Alerts.ContainsKey("level_drop"))
        {
            bool opRecorded = Db.Query(c, "SELECT 1 FROM operations WHERE (from_vessel_id=$1 OR to_vessel_id=$1) AND created_at>=$2",
                v.Id, (StartedUtc > DateTime.UtcNow.AddHours(-1) ? StartedUtc : DateTime.UtcNow.AddHours(-1)).ToString("o")).Count > 0;
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

        if (Scenario == "missing" && v.Fermenting && v.Code == MissingTarget(c))
            Set("missing_brix", true, "amber", () => $"{v.Name} has no Initial Brix — potential alcohol can't be calculated.");
        return outp;
    }

    /// Rule names: temperature keeps "temp_high" (as before) plus "temp_low"; the others are e.g. "density_low", "level_high".
    public static string RangeRule(string metric, string dir) => (metric == "temperature" ? "temp" : metric) + (dir == "too low" ? "_low" : "_high");

    public object State() => new
    {
        on = On, scenario = Scenario, scenario_title = ScenarioTitles[Scenario], sim_time = SimTime.ToString("yyyy-MM-ddTHH:mm"),
        // temp_max = F-2's allowed maximum from config/ranges.json (the chart's dashed line)
        thresholds = new { temp_max = V("F-2")?.Ranges.GetValueOrDefault("temperature")?.Max ?? 30, level_drop_l = S("live_level_drop_l"), stuck_min_sg = S("live_stuck_min_sg") },
        range_alerts = Vessels.SelectMany(v => v.Range.Alerts.Values.Select(a => new
        {
            vessel_code = v.Code, vessel_name = v.Name, batch = v.Batch, metric = a.Metric, direction = a.Direction, value = a.Value,
            min = a.Range.Min, max = a.Range.Max, unit = a.Range.Unit, since = a.Since, source = "SIMULATED",
        })),
        vessels = Vessels.Where(v => v.Streaming).Select(v => new
        {
            id = v.Id, code = v.Code, winery = v.Winery, name = v.Name, type = v.Type, fermenting = v.Fermenting,
            temp = Math.Round(v.Temp, 1), level = Math.Round(v.Level, 1), capacity = v.Capacity, density = v.Density == null ? (double?)null : Math.Round(v.Density.Value, 4),
            status = v.Status, alerts = v.Alerts, batch = v.Batch, wine_color = v.WineColor,
            ranges = v.Ranges.ToDictionary(kv => kv.Key, kv => new
            {
                min = kv.Value.Min, max = kv.Value.Max, unit = kv.Value.Unit, from = kv.Value.From,
                in_range = v.Range.InRange.TryGetValue(kv.Key, out var ok) ? ok : (bool?)null,
            }),
        }),
        source = "SIMULATED",
    };

    /// A real sensor (POST /api/sensors/reading) feeds the same stream; its events are tagged SENSOR, not SIMULATED.
    public static LiveEvent SensorEvent(long vesselId, string code, string? name, double litres) => new()
    {
        Kind = "reading", VesselId = vesselId, VesselCode = code, VesselName = name ?? code, Metric = "level", Value = litres, Unit = "L", Source = "SENSOR",
        CreatedAt = DateTime.UtcNow.ToString("o"), SimTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm"),
    };
}

/// Fan-out of live messages to every open SSE stream.
public sealed class LiveHub
{
    readonly object gate = new();
    readonly List<Channel<string>> subs = [];
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public (ChannelReader<string> reader, Action dispose) Subscribe()
    {
        var ch = Channel.CreateBounded<string>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });
        lock (gate) subs.Add(ch);
        return (ch.Reader, () => { lock (gate) subs.Remove(ch); ch.Writer.TryComplete(); });
    }

    public void Publish(string type, object payload)
    {
        var msg = $"event: {type}\ndata: {JsonSerializer.Serialize(payload, Json)}\n\n";
        lock (gate) foreach (var s in subs) s.Writer.TryWrite(msg);
    }
}

/// Background simulator: one tick every 3 s while Live is ON.
public sealed class LiveService(LiveHub hub) : BackgroundService
{
    public LiveSim Sim { get; } = new();
    readonly object gate = new();

    public object Control(Func<SqliteConnection, SqliteTransaction, object> f)
    {
        lock (gate) return Db.Write(f);
    }

    public void TurnOn()
    {
        lock (gate)
        {
            if (Sim.On) return;
            using (var c = Db.Open()) Sim.Start(c);
            Sim.On = true;
        }
        hub.Publish("state", Sim.State());
    }

    public void TurnOff()
    {
        lock (gate) Sim.On = false;
        hub.Publish("state", Sim.State());
    }

    public void SetScenario(string s)
    {
        TurnOn();
        lock (gate) using (var c = Db.Open()) Sim.SetScenario(c, s);
        Tick(); // react immediately
    }

    public void Tick()
    {
        List<LiveEvent> evs;
        lock (gate)
        {
            if (!Sim.On) return;
            evs = Db.Write((c, tx) =>
            {
                var e = Sim.Tick(c, tx);
                // simulated readings are kept short; notifications stay for the feed
                Db.Exec(c, tx, "DELETE FROM live_events WHERE kind='reading' AND id < (SELECT COALESCE(MAX(id),0) - 3000 FROM live_events)");
                return e;
            });
        }
        hub.Publish("tick", new { state = Sim.State(), readings = evs.Where(e => e.Kind == "reading"), notifications = evs.Where(e => e.Kind == "notification") });
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try { await Task.Delay(3000, stop); } catch (TaskCanceledException) { break; }
            try { Tick(); } catch (Exception e) { Console.Error.WriteLine("live tick failed: " + e.Message); }
        }
    }
}
