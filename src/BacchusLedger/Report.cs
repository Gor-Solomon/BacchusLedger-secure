using BacchusLedger.Contracts.Report;
using Microsoft.Data.Sqlite;

namespace BacchusLedger;

/// Ledger report: one batch from vineyard to bottle as a double-entry ledger. Read-only (SELECTs only).
/// Litres and losses come from LossEngine.Analyze; this class only books them as debit/credit pairs.
/// Simulated live events are listed separately and never enter the ledger.
public static class LedgerReport
{
    public const string Reception = "Winery reception (grapes)", Losses = "Losses", Unexplained = "Unexplained difference",
        Bottled = "Bottled wine", Warehouse = "Finished goods warehouse";

    /// One movement: debit = the place that receives, credit = the place that gives. Same amount on both lines,
    /// except a transformation (pressing: kg of grapes → litres of must), which carries its own credit amount and unit.
    public sealed record Entry(int No, string Date, string Kind, string Text, string Debit, string Credit, double Amount, string Unit,
        double? CreditAmount = null, string? CreditUnit = null, string? Ratio = null, string? Source = null, long? OpId = null, string? Ref = null);

    public sealed record Account(string Name, string Unit, double Debit, double Credit)
    {
        public double Balance => Math.Round(Debit - Credit, 2);
    }

    public sealed record Balance(double? GrapesKg, double HarvestedKg, double PressedKg, double GrapesLeftKg, double? KgGap,
        double InL, double BottledL, double InVesselsL, double LossesL, double GapL, bool HasLitres, bool EngineMatches, bool Balanced);

    public sealed record Ledger(List<Entry> Entries, List<Account> Accounts, Balance Balance, LossEngine.Result Engine);

    static double R(double v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    static string V(string? code, string? label) => code == null ? "Vessel not recorded" : Fmt.Vessel(code, label);
    static string Grapes(string? code, string? label) => $"{V(code, label)} · grapes";

    /// Typical cause per stage (the record has the litres, not the cause).
    static string Cause(LossEngine.Stage s) => s.StageKey switch
    {
        "racking_first" or "racking" => "lees (racking)",
        "filtration" => "filtration",
        "bottling" => "bottling",
        "stock_check" => "evaporation / sampling (stock check)",
        _ => s.Type,
    };

    public static Ledger Build(SqliteConnection c, long batchId, Dictionary<string, string>? settings = null)
    {
        var b = Db.Query(c, "SELECT grapes_kg FROM batches WHERE id=$1", batchId)[0];
        var eng = LossEngine.Analyze(c, batchId, settings, compare: false);
        var entries = new List<Entry>();
        var acc = new Dictionary<(string, string), (double dr, double cr)>();
        void Book(string date, string kind, string text, string debit, string credit, double amount, string unit,
            double? crAmount = null, string? crUnit = null, string? ratio = null, string? source = null, long? op = null, string? reference = null)
        {
            entries.Add(new(entries.Count + 1, date, kind, text, debit, credit, R(amount), unit, crAmount == null ? null : R(crAmount.Value), crUnit, ratio, source, op, reference ?? (op != null ? $"operations:{op}" : null)));
            var d = acc.GetValueOrDefault((debit, unit)); acc[(debit, unit)] = (d.dr + amount, d.cr);
            var cu = crUnit ?? unit; var k = acc.GetValueOrDefault((credit, cu)); acc[(credit, cu)] = (k.dr, k.cr + (crAmount ?? amount));
        }
        double Bal(string name, string unit) => acc.TryGetValue((name, unit), out var x) ? x.dr - x.cr : 0;

        // Harvest: vineyard plot → winery reception (kg). Transport is not recorded separately in the data.
        var origins = Db.Query(c, """
            SELECT d.id, d.date, bo.kg, d.source, p.code AS plot, p.municipality, f.name AS farmer FROM batch_origins bo
            JOIN deliveries d ON d.id=bo.delivery_id JOIN plots p ON p.id=d.plot_id LEFT JOIN farmers f ON f.id=d.farmer_id
            WHERE bo.batch_id=$1 ORDER BY d.date, d.id
            """, batchId);
        var firstDate = eng.Stages.FirstOrDefault()?.Date ?? "";
        foreach (var o in origins)
            Book((string)o["date"]!, "harvest", $"Delivery from plot {o["plot"]} ({o["municipality"]})", Reception,
                $"Vineyard {o["plot"]} · {o["farmer"] ?? "farmer not recorded"}", Db.D(o["kg"]) ?? 0, "kg", source: o["source"] as string, reference: $"deliveries:{o["id"]}");
        var grapesKg = Db.D(b["grapes_kg"]);
        if (origins.Count == 0 && grapesKg is > 0)
            Book(firstDate, "harvest", "Grapes received (plots not recorded)", Reception, "Vineyard (plots not recorded)", grapesKg.Value, "kg", source: "Dataset A2");
        double harvested = acc.Where(a => a.Key.Item2 == "kg" && a.Key.Item1.StartsWith("Vineyard")).Sum(a => a.Value.cr);

        foreach (var s in eng.Stages)
        {
            void Loss(string vessel)
            {
                if (s.LossL is not { } l) return;
                if (l > 0) Book(s.Date, "loss", $"Loss at {s.StageKey.Replace('_', ' ')}: {Cause(s)}", Losses, vessel, l, "L", source: "Calculated", op: s.OpId);
                else if (l < 0) Book(s.Date, "unexplained", $"More wine than the record explains at {s.StageKey.Replace('_', ' ')}", vessel, Unexplained, -l, "L", source: "Calculated", op: s.OpId);
            }
            switch (s.Type)
            {
                case "intake":
                {
                    var kg = Bal(Reception, "kg");
                    if (kg > 0) Book(s.Date, "intake", $"Grapes into {V(s.To, s.ToLabel)}" + (s.Note != null ? $" ({s.Note})" : ""), Grapes(s.To, s.ToLabel), Reception, kg, "kg", source: s.Source, op: s.OpId);
                    break;
                }
                case "separation":
                {
                    // Transformation: all grapes in the source vessel (or still at reception) become the litres measured.
                    var from = Grapes(s.From, s.FromLabel);
                    var kg = Bal(from, "kg");
                    if (kg <= 0) { from = Reception; kg = Bal(Reception, "kg"); }
                    var l = s.AfterL ?? 0;
                    var ratio = kg > 0 ? $"{l / kg * 1000:N1} L per tonne ({l / kg:0.000} L/kg)" : "no grapes recorded";
                    Book(s.Date, "transformation", $"Pressing / separation: grapes → wine into {V(s.To, s.ToLabel)}", V(s.To, s.ToLabel), kg > 0 ? from : "Grapes (not recorded)",
                        l, "L", Math.Max(kg, 0), "kg", ratio, s.Source, s.OpId);
                    break;
                }
                case "racking" or "filtration" or "transfer":
                    if (s.AfterL is > 0) Book(s.Date, "move", $"{char.ToUpper(s.Type[0])}{s.Type[1..]} {s.From} → {s.To}", V(s.To, s.ToLabel), V(s.From, s.FromLabel), s.AfterL.Value, "L", source: s.Source, op: s.OpId);
                    Loss(V(s.From, s.FromLabel));
                    break;
                case "stock_check":
                    if (s.LossL is 0 or null) Book(s.Date, "stock check", $"Stock check {s.From}: measured {s.AfterL:N0} L = book {s.BeforeL:N0} L, no difference", Losses, V(s.From, s.FromLabel), 0, "L", source: s.Source, op: s.OpId);
                    Loss(V(s.From, s.FromLabel));
                    break;
                case "bottling":
                    Book(s.Date, "bottling", $"Bottling {s.Bottles:N0} × {(s.BottleMl ?? 750) / 1000:0.00#} L from {s.From}" + (s.RemainderL is > 0 ? $" ({s.RemainderL:N0} L stays in {s.From})" : ""),
                        Bottled, V(s.From, s.FromLabel), s.AfterL ?? 0, "L", source: s.Source, op: s.OpId);
                    Loss(V(s.From, s.FromLabel));
                    break;
                case "warehouse":
                {
                    var l = (s.Bottles ?? 0) * (s.BottleMl ?? 750) / 1000.0;
                    if (l > 0) Book(s.Date, "warehouse", $"{s.Bottles:N0} bottles to the warehouse", Warehouse, Bottled, l, "L", source: s.Source, op: s.OpId);
                    break;
                }
            }
        }

        var accounts = acc.Select(a => new Account(a.Key.Item1, a.Key.Item2, R(a.Value.dr), R(a.Value.cr))).ToList();
        double inL = entries.Where(e => e.Kind == "transformation").Sum(e => e.Amount);
        double bottled = R(Bal(Bottled, "L") + Bal(Warehouse, "L"));
        var special = new[] { Bottled, Warehouse, Losses, Unexplained };
        var vesselAcc = accounts.Where(a => a.Unit == "L" && !special.Contains(a.Name)).ToList();
        double inVessels = R(vesselAcc.Sum(a => a.Balance));
        double losses = R(Bal(Losses, "L"));
        double gap = R(inL - bottled - inVessels - losses);
        double pressed = entries.Where(e => e.Kind == "transformation").Sum(e => e.CreditAmount ?? 0);
        double left = R(accounts.Where(a => a.Unit == "kg" && !a.Name.StartsWith("Vineyard")).Sum(a => a.Balance));
        double? kgGap = grapesKg == null || origins.Count == 0 ? null : R(grapesKg.Value - harvested);
        // Cross-check with LossEngine: same total loss (positive losses minus unexplained gains) and the same litres per vessel.
        bool engineOk = Math.Abs(losses + gap - eng.TotalLossL) < 0.01
            && Math.Abs(inVessels - eng.LitresNow) < 0.01;
        bool hasL = inL > 0;
        var bal = new Balance(grapesKg, R(harvested), R(pressed), left, kgGap, R(inL), bottled, inVessels, losses, gap, hasL, engineOk,
            Math.Abs(gap) < 0.005 && (kgGap == null || Math.Abs(kgGap.Value) < 0.005) && engineOk);
        return new(entries, accounts, bal, eng);
    }

    /// GET /api/report — the whole printable report (ReportResponse), or VesselReportResponse when only a vessel is given;
    /// null when the batch does not exist. vessel = "W01/F-2" (Live board) adds the SIMULATED section.
    public static object? Report(SqliteConnection c, string? batchCode, string? vessel)
    {
        SimulatedSectionDto? simulated = null;
        string? vesselName = null;
        if (vessel != null && vessel.Split('/') is [var w, var code])
        {
            var vr = Db.Query(c, "SELECT v.id, v.code, v.cellar_label, v.name FROM vessels v JOIN wineries x ON x.id=v.winery_id WHERE x.code=$1 AND v.code=$2", w, code).FirstOrDefault();
            if (vr != null)
            {
                vesselName = V((string)vr["code"]!, vr["cellar_label"] as string);
                var alerts = Db.Query(c, """
                    SELECT created_at, sim_time, level, rule, message, scenario, source, acknowledged FROM live_events
                    WHERE kind='notification' AND vessel_id=$1 ORDER BY id DESC LIMIT 50
                    """, vr["id"]).Select(a => new SimulatedAlertDto(a["created_at"] as string, a["sim_time"] as string, a["level"] as string, a["rule"] as string,
                        a["message"] as string, a["scenario"] as string, a["source"] as string, Db.L(a["acknowledged"]))).ToList();
                simulated = new SimulatedSectionDto(vesselName, "SIMULATED", "Simulated live data (live_events). Not part of the ledger, the balance or any journal.",
                    alerts, (long)Db.Query(c, "SELECT COUNT(*) n FROM live_events WHERE kind='reading' AND vessel_id=$1", vr["id"])[0]["n"]!);
            }
        }
        var br = batchCode == null ? null : Db.Query(c, """
            SELECT b.*, w.code AS winery, w.name AS winery_name FROM batches b JOIN wineries w ON w.id=b.winery_id WHERE b.code=$1
            """, batchCode).FirstOrDefault();
        if (br == null)
            return batchCode != null ? null : new VesselReportResponse(null, vesselName, simulated, DateTime.UtcNow.ToString("o"));

        long id = (long)br["id"]!;
        var l = Build(c, id);
        var eng = l.Engine;
        var timeline = new List<TimelineRowDto>();
        var harvest = l.Entries.Where(e => e.Kind == "harvest").ToList();
        if (harvest.Count > 0)
            timeline.Add(new(harvest[0].Date, "Harvest", $"{harvest.Count} {(harvest.Count == 1 ? "delivery" : "deliveries")} → reception", $"{harvest.Sum(e => e.Amount):N0} kg"));
        foreach (var s in eng.Stages)
            timeline.Add(new(s.Date, s.StageKey.Replace('_', ' '),
                s.Type == "stock_check" ? s.From : s.From != null && s.To != null ? $"{s.From} → {s.To}" : s.To ?? s.From ?? "—",
                s.Type switch
                {
                    "intake" => l.Entries.FirstOrDefault(e => e.OpId == s.OpId)?.Amount is { } kg ? $"{kg:N0} kg" : s.Note ?? "—",
                    "bottling" or "warehouse" => $"{s.Bottles:N0} bottles" + (s.AfterL != null ? $" · {s.AfterL:N0} L" : ""),
                    _ => s.AfterL != null ? $"{s.AfterL:N0} L" + (s.LossL is > 0 ? $" (−{s.LossL:N0} L)" : "") : "—",
                }));
        var lab = Db.Query(c, """
            SELECT l.id, l.date, v.code AS vessel, l.stage, l.sugar_gl, l.temp_c, l.ph, l.ta_gl, l.rs_gl, l.note, l.source FROM lab l
            LEFT JOIN vessels v ON v.id=l.vessel_id WHERE l.batch_id=$1 ORDER BY l.date, l.id
            """, id).Select(r => new ReportLabRowDto((long)r["id"]!, r["date"] as string, r["vessel"] as string, r["stage"] as string, Db.D(r["sugar_gl"]), Db.D(r["temp_c"]),
                Db.D(r["ph"]), Db.D(r["ta_gl"]), Db.D(r["rs_gl"]), r["note"] as string, r["source"] as string)).ToList();
        // Readings that are not copies of the lab sheet above (entered values, e.g. initial Brix, hydrometer).
        var readings = Db.Query(c, """
            SELECT r.id, r.taken_at, v.code AS vessel, r.type, r.value, r.unit, r.instrument, r.person, r.note, r.source FROM readings r
            LEFT JOIN vessels v ON v.id=r.vessel_id
            WHERE r.batch_id=$1 ORDER BY r.taken_at, r.id
            """, id).Select(r => new ReportReadingDto((long)r["id"]!, r["taken_at"] as string, r["vessel"] as string, r["type"] as string, Db.D(r["value"]), r["unit"] as string,
                r["instrument"] as string, r["person"] as string, r["note"] as string, r["source"] as string)).ToList();
        var batch = new ReportBatchDto(br["code"] as string, br["name"] as string, br["winery"] as string, br["winery_name"] as string, br["wine_type"] as string,
            br["technology"] as string, br["variety"] as string, Db.L(br["vintage"]), Db.D(br["abv"]), br["status"] as string, Db.D(br["grapes_kg"]), br["source_note"] as string);
        var entries = l.Entries.Select(e => new LedgerEntryDto(e.No, e.OpId, e.Ref, e.Date, e.Kind, e.Text, e.Source, e.Ratio,
            new LedgerSideDto(e.Debit, e.Amount, e.Unit), new LedgerSideDto(e.Credit, e.CreditAmount ?? e.Amount, e.CreditUnit ?? e.Unit))).ToList();
        var accounts = l.Accounts.Select(a => new LedgerAccountDto(a.Name, a.Unit, a.Debit, a.Credit, a.Balance)).ToList();
        var b = l.Balance;
        var balance = new LedgerBalanceDto(b.GrapesKg, b.HarvestedKg, b.PressedKg, b.GrapesLeftKg, b.KgGap, b.InL, b.BottledL, b.InVesselsL, b.LossesL, b.GapL,
            b.HasLitres, eng.TotalLossL, b.EngineMatches, b.Balanced, eng.Checks.Select(x => new BalanceCheckDto(x.Label, x.Status, x.Detail)).ToList());
        return new ReportResponse(batch, vesselName, timeline, entries, accounts, balance, lab, readings, simulated, DateTime.UtcNow.ToString("o"));
    }
}

/// Selftest: the ledger of the demo batches balances, and building it changes nothing.
public static class LedgerSelfTest
{
    public static void Run(Action<string, string, string, string, bool> Add)
    {
        const string g = "ledger report";
        try
        {
            using var c = Db.Open();
            foreach (var (code, inL, outL, loss) in new[] { ("W01-B1-2025", 10100.0, 9650.0, 450.0), ("W03-B3-2025", 1230.0, 1182.0, 48.0) })
            {
                var id = Db.Query(c, "SELECT id FROM batches WHERE code=$1", code).FirstOrDefault()?["id"];
                if (id == null) { Add(g, $"{code} ledger balances", "present", "missing", false); continue; }
                var b = LedgerReport.Build(c, (long)id).Balance;
                Add(g, $"{code} ledger balances: in = out + losses, no unexplained difference",
                    $"{inL:N0} = {outL:N0} + {loss:N0}, gap 0", $"{b.InL:N0} = {b.BottledL + b.InVesselsL:N0} + {b.LossesL:N0}, gap {b.GapL:0.##}",
                    b.Balanced && b.InL == inL && b.BottledL + b.InVesselsL == outL && b.LossesL == loss);
            }

            // Run both demo players to the end in a rolled-back transaction, then check the demo copies' ledgers.
            var demo = Db.Probe((c2, tx) =>
            {
                var r = new List<string>();
                foreach (var script in new[] { "w01", "w03" })
                {
                    DemoScript.ReplayAll(c2, tx, script);
                    var code = script == "w03" ? DemoScript.W03Code : DemoScript.W01Code;
                    var b = LedgerReport.Build(c2, (long)Db.Query(c2, "SELECT id FROM batches WHERE code=$1", code)[0]["id"]!).Balance;
                    r.Add($"{code} {(b.Balanced ? "balanced" : $"gap {b.GapL} L")}");
                }
                return r;
            });
            Add(g, "Demo copies (A→Z and Qvevri players run to the end) balance", "both balanced", string.Join(", ", demo), demo.All(x => x.EndsWith("balanced")));

            // Simulated live events never enter the ledger; building the report writes nothing.
            var (same, sim, unchanged) = Db.Probe((c2, tx) =>
            {
                string Counts() => string.Join(",", new[] { "operations", "readings", "lab", "batches", "deliveries", "batch_origins", "live_events" }
                    .Select(t => Db.Query(c2, $"SELECT COUNT(*) n FROM {t}")[0]["n"]));
                string Ledger() => System.Text.Json.JsonSerializer.Serialize(LedgerReport.Report(c2, "W03-B3-2025", null), LiveHub.Json);
                var before = Ledger();
                var sim0 = new LiveSim(seed: 3) { On = true };
                sim0.Start(c2, new Dictionary<string, string>(Db.DefaultSettings));
                sim0.SetScenario(c2, "hot");
                for (int i = 0; i < 6; i++) sim0.Tick(c2, tx);
                var c0 = Counts();
                var withSim = System.Text.Json.JsonSerializer.Serialize(LedgerReport.Report(c2, "W03-B3-2025", "W03/Q-3"), LiveHub.Json);
                var after = Ledger();
                return (StripTime(before) == StripTime(after), withSim.Contains("\"source\":\"SIMULATED\""), Counts() == c0);
            });
            Add(g, "Simulated live events stay out of the ledger (separate SIMULATED section)", "ledger identical", same && sim ? "ledger identical" : "changed", same && sim);
            Add(g, "Building the report writes nothing", "unchanged", unchanged ? "unchanged" : "changed", unchanged);
        }
        catch (Exception e) { Add(g, "ledger report", "ok", e.Message, false); }
    }

    static string StripTime(string json) => System.Text.RegularExpressions.Regex.Replace(json, "\"generated_at\":\"[^\"]*\"", "");
}
