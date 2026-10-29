using Microsoft.Data.Sqlite;

namespace BacchusLedger;

/// Section 6: mass balance. Replays a batch's operations in date order, tracking litres per vessel.
public static class LossEngine
{
    public sealed class Op
    {
        public long Id;
        public string Date = "", Type = "";
        public long? FromId, ToId;
        public string? FromCode, ToCode, FromLabel, ToLabel, Method, Note, Source, Person;
        public double? Litres, Ml, Remainder, Dip;
        public int? Bottles;
    }

    public sealed class Stage
    {
        public long OpId { get; set; }
        public string Date { get; set; } = "";
        public string Type { get; set; } = "";
        public string StageKey { get; set; } = "";
        public string? From { get; set; }
        public string? To { get; set; }
        public string? FromLabel { get; set; }
        public string? ToLabel { get; set; }
        public double? BeforeL { get; set; }
        public double? AfterL { get; set; }
        public double? RemainderL { get; set; }
        public double? LossL { get; set; }
        public double? LossPct { get; set; }
        public double? NormPct { get; set; }
        public double? NormL { get; set; }
        public double? ExcessL { get; set; }
        public string Status { get; set; } = "n/a"; // ok | above | negative | n/a
        public int? Bottles { get; set; }
        public double? BottleMl { get; set; }
        public string? Method { get; set; }
        public double? DipCm { get; set; }
        public string? Note { get; set; }
        public string? Source { get; set; }
        public string? Person { get; set; }
        public StageComparison? Other { get; set; }
    }

    /// The same stage at the other winery (W01 vs W03).
    public sealed record StageComparison(string Winery, string Batch, double? LossL, double? LossPct, string Status, string? Source);

    public sealed class Check
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Status { get; set; } = "pass"; // pass | fail | info | na
        public string Detail { get; set; } = "";
        public string Source { get; set; } = "Calculated";
    }

    public sealed class Result
    {
        public long BatchId { get; set; }
        public string BatchCode { get; set; } = "";
        public string Winery { get; set; } = "";
        public List<Stage> Stages { get; set; } = [];
        public List<Check> Checks { get; set; } = [];
        public double? FirstLitres { get; set; }
        public double BottledL { get; set; }
        public int Bottles { get; set; }
        public double RemainderL { get; set; }
        public double TotalLossL { get; set; }
        public double? TotalDifferenceL { get; set; }
        public double LitresNow { get; set; }
        public double LossValueUsd { get; set; }
        public double ExcessL { get; set; }
        public Dictionary<string, double> VesselLitres { get; set; } = [];
    }

    public static readonly string[] NormKeys = ["racking_first", "racking", "filtration", "bottling", "stock_check", "transfer"];

    public static Dictionary<string, double> Norms(Dictionary<string, string> s) =>
        NormKeys.ToDictionary(k => k, k => Db.SettingD(s, "norm_" + k));

    public static List<Op> LoadOps(SqliteConnection c, long batchId) =>
        Db.Query(c, """
            SELECT o.*, vf.code AS from_code, vt.code AS to_code, vf.cellar_label AS from_label, vt.cellar_label AS to_label FROM operations o
            LEFT JOIN vessels vf ON vf.id=o.from_vessel_id LEFT JOIN vessels vt ON vt.id=o.to_vessel_id
            WHERE o.batch_id=$1 ORDER BY o.date, o.id
            """, batchId).Select(r => new Op
        {
            Id = (long)r["id"]!, Date = (string)r["date"]!, Type = (string)r["type"]!,
            FromId = Db.L(r["from_vessel_id"]), ToId = Db.L(r["to_vessel_id"]),
            FromCode = r["from_code"] as string, ToCode = r["to_code"] as string, FromLabel = r["from_label"] as string, ToLabel = r["to_label"] as string,
            Litres = Db.D(r["litres"]), Bottles = r["bottles"] == null ? null : (int)(long)r["bottles"]!,
            Ml = Db.D(r["bottle_ml"]), Remainder = Db.D(r["remainder_l"]), Dip = Db.D(r["dip_cm"]),
            Method = r["method"] as string, Note = r["note"] as string, Source = r["source"] as string, Person = r["person"] as string,
        }).ToList();

    static double R(double v, int d = 2) => Math.Round(v, d, MidpointRounding.AwayFromZero);

    /// Core replay over an op list (pure — used for preview, selftest and pages).
    public static Result Replay(List<Op> ops, Dictionary<string, double> norms, double valuePerLitre)
    {
        var res = new Result();
        var vessels = new Dictionary<string, double>();
        double Get(string? v) => v != null && vessels.TryGetValue(v, out var l) ? l : 0;
        bool firstRackingSeen = false;
        var giveOutFails = new List<string>();
        var bottleChecks = new List<(bool ok, string text)>();

        foreach (var o in ops.OrderBy(o => o.Date, StringComparer.Ordinal).ThenBy(o => o.Id))
        {
            var st = new Stage
            {
                OpId = o.Id, Date = o.Date, Type = o.Type, StageKey = o.Type, From = o.FromCode, To = o.ToCode, FromLabel = o.FromLabel, ToLabel = o.ToLabel,
                Bottles = o.Bottles, BottleMl = o.Ml, Method = o.Method, DipCm = o.Dip, Note = o.Note, Source = o.Source, Person = o.Person,
            };
            switch (o.Type)
            {
                case "separation":
                    st.AfterL = o.Litres;
                    if (o.ToCode != null) vessels[o.ToCode] = Get(o.ToCode) + (o.Litres ?? 0);
                    res.FirstLitres ??= o.Litres;
                    break;
                case "racking" or "filtration" or "transfer":
                {
                    if (o.Type == "racking") { st.StageKey = firstRackingSeen ? "racking" : "racking_first"; firstRackingSeen = true; }
                    double before = Get(o.FromCode), outL = o.Litres ?? 0, rem = o.Remainder ?? 0;
                    st.BeforeL = before; st.AfterL = outL; st.RemainderL = rem;
                    st.LossL = R(before - outL - rem);
                    if (outL + rem > before + 1e-9) giveOutFails.Add($"{o.Date} {o.FromCode}: gives {outL + rem:N0} L, holds {before:N0} L");
                    if (o.FromCode != null) vessels[o.FromCode] = rem;
                    if (o.ToCode != null) vessels[o.ToCode] = Get(o.ToCode) + outL;
                    break;
                }
                case "stock_check":
                {
                    var v = o.FromCode ?? o.ToCode;
                    double before = Get(v), measured = o.Litres ?? 0;
                    st.From = v; st.FromLabel = o.FromCode != null ? o.FromLabel : o.ToLabel; st.To = null; st.ToLabel = null; st.BeforeL = before; st.AfterL = measured;
                    st.LossL = R(before - measured);
                    if (v != null) vessels[v] = measured;
                    break;
                }
                case "bottling":
                {
                    double before = Get(o.FromCode), rem = o.Remainder ?? 0;
                    double outL = (o.Bottles ?? 0) * (o.Ml ?? 750) / 1000.0;
                    st.BeforeL = before; st.AfterL = R(outL); st.RemainderL = rem;
                    st.LossL = R(before - outL - rem);
                    if (outL + rem > before + 1e-9) giveOutFails.Add($"{o.Date} {o.FromCode}: gives {outL + rem:N0} L, holds {before:N0} L");
                    if (o.FromCode != null) vessels[o.FromCode] = rem;
                    res.BottledL += outL; res.Bottles += o.Bottles ?? 0; res.RemainderL += rem;
                    // Bottle check: bottles x bottle size = litres recorded as bottled
                    double recorded = o.Litres ?? outL;
                    bottleChecks.Add((Math.Abs(recorded - outL) < 0.5,
                        $"{o.Bottles:N0} × {(o.Ml ?? 750) / 1000.0:0.00#} = {outL:N0} L" + (o.Litres != null ? $" (recorded {o.Litres:N0} L)" : "")));
                    break;
                }
            }
            if (st.LossL != null && st.BeforeL is > 0)
            {
                double before = st.BeforeL.Value;
                st.LossPct = R(st.LossL.Value / before * 100);
                if (norms.TryGetValue(st.StageKey, out var norm))
                {
                    st.NormPct = norm;
                    st.NormL = R(norm / 100 * before);
                    st.ExcessL = R(Math.Max(0, st.LossL.Value - norm / 100 * before));
                    st.Status = st.LossL < 0 ? "negative" : st.ExcessL > 0.5 ? "above" : "ok";
                }
            }
            else if (st.LossL != null) st.Status = st.LossL < 0 ? "negative" : "n/a";
            res.Stages.Add(st);
        }

        res.TotalLossL = R(res.Stages.Where(s => s.LossL != null).Sum(s => s.LossL!.Value));
        res.ExcessL = R(res.Stages.Where(s => s.ExcessL != null).Sum(s => s.ExcessL!.Value));
        res.BottledL = R(res.BottledL);
        res.TotalDifferenceL = res.FirstLitres == null ? null : R(res.FirstLitres.Value - res.BottledL - (res.Bottles > 0 ? 0 : vessels.Values.Sum()));
        res.VesselLitres = vessels.Where(kv => kv.Value > 1e-9).ToDictionary(kv => kv.Key, kv => R(kv.Value));
        res.LitresNow = R(vessels.Values.Sum());
        res.LossValueUsd = R(res.TotalLossL * valuePerLitre);

        foreach (var (ok, text) in bottleChecks)
            res.Checks.Add(new Check { Key = "bottle", Label = "Bottle check: bottles × bottle size = litres bottled", Status = ok ? "pass" : "fail", Detail = text });
        res.Checks.Add(new Check
        {
            Key = "give_out", Label = "A vessel cannot give out more than it holds",
            Status = ops.Any(o => o.Type is "racking" or "filtration" or "transfer" or "bottling") ? (giveOutFails.Count == 0 ? "pass" : "fail") : "na",
            Detail = giveOutFails.Count == 0 ? "All transfers within vessel contents" : string.Join("; ", giveOutFails),
        });
        return res;
    }

    public static Result Analyze(SqliteConnection c, long batchId, Dictionary<string, string>? settings = null, Op? extra = null, bool compare = true, Dictionary<string, double>? norms = null)
    {
        settings ??= Db.Settings();
        norms ??= Norms(settings);
        var b = Db.Query(c, "SELECT b.*, w.code AS winery_code FROM batches b JOIN wineries w ON w.id=b.winery_id WHERE b.id=$1", batchId)[0];
        var ops = LoadOps(c, batchId);
        if (extra != null) ops.Add(extra);
        var res = Replay(ops, norms, Db.SettingD(settings, "value_per_litre_usd"));
        res.BatchId = batchId; res.BatchCode = (string)b["code"]!; res.Winery = (string)b["winery_code"]!;

        // Same stage at the other winery (W01 vs W03) — pure dataset fact
        var refCode = res.Winery switch { "W01" => "W03-B3-2025", "W03" => "W01-B1-2025", _ => null };
        if (compare && refCode != null && Db.Query(c, "SELECT id FROM batches WHERE code=$1", refCode) is { Count: > 0 } rr)
        {
            var other = Analyze(c, (long)rr[0]["id"]!, settings, null, false, norms);
            foreach (var s in res.Stages)
            {
                var m = other.Stages.FirstOrDefault(x => x.StageKey == s.StageKey && x.LossL != null);
                if (m != null && s.LossL != null)
                    s.Other = new StageComparison(other.Winery, other.BatchCode, m.LossL, m.LossPct, m.Status, m.Source);
            }
        }

        // Origin checks
        var origins = Db.Query(c, """
            SELECT bo.kg, p.code AS plot, p.municipality FROM batch_origins bo
            JOIN deliveries d ON d.id=bo.delivery_id JOIN plots p ON p.id=d.plot_id WHERE bo.batch_id=$1
            """, batchId);
        if (origins.Count == 0)
        {
            res.Checks.Add(new Check { Key = "pdo", Label = "Plot municipality is inside the Kakheti PDO zone", Status = "na", Detail = "No plots recorded for this batch", Source = "Kakheti PDO spec" });
            res.Checks.Add(new Check { Key = "origin", Label = "Origin completeness: batch kg = sum of its deliveries", Status = "na", Detail = b["grapes_kg"] == null ? "No grape intake recorded for this batch" : $"{Db.D(b["grapes_kg"]):N0} kg in Dataset A2 — no deliveries recorded (the dataset has no farmers)", Source = b["grapes_kg"] == null ? "Calculated" : "Dataset A2" });
        }
        else
        {
            var outside = origins.Where(o => !Seed.PdoMunicipalities.Contains(o["municipality"] as string ?? "")).Select(o => $"{o["plot"]} ({o["municipality"]})").ToList();
            var munis = string.Join(", ", origins.Select(o => o["municipality"] as string).Distinct());
            res.Checks.Add(new Check
            {
                Key = "pdo", Label = "Plot municipality is inside the Kakheti PDO zone", Status = outside.Count == 0 ? "pass" : "fail",
                Detail = outside.Count == 0 ? $"{munis} is in the §3 list" : "Outside zone: " + string.Join(", ", outside), Source = "Kakheti PDO spec",
            });
            double kg = Db.D(b["grapes_kg"]) ?? 0, sum = origins.Sum(o => Db.D(o["kg"]) ?? 0);
            res.Checks.Add(new Check
            {
                Key = "origin", Label = "Origin completeness: batch kg = sum of its deliveries", Status = Math.Abs(kg - sum) < 0.01 ? "pass" : "fail",
                Detail = $"{kg:N0} = " + string.Join(" + ", origins.Select(o => $"{Db.D(o["kg"]):N0}")) + (Math.Abs(kg - sum) < 0.01 ? "" : $" = {sum:N0}"),
            });
        }

        if ((string?)b["technology"] == "qvevri")
        {
            var sep = res.Stages.FirstOrDefault(s => s.Type == "separation");
            res.Checks.Add(new Check
            {
                Key = "qvevri_rule", Label = "Qvevri rule (information)", Status = "info", Source = "Challenge Book",
                Detail = "Challenge Book: by law 'qvevri wine' must be fermented and matured in the qvevri up to 31 December of the year of pressing. " +
                         (sep != null ? $"This batch was separated on {Fmt.Date(sep.Date)} — please verify the label category." : "No separation recorded yet — please verify the label category."),
            });
        }
        return res;
    }
}

public static class Fmt
{
    public static string Date(string iso) => iso.Length >= 10 ? $"{iso[8..10]}.{iso[5..7]}.{iso[..4]}" : iso;
    public static string DateTime(string iso) => iso.Length >= 16 ? $"{Date(iso)} {iso[11..16]}" : Date(iso);
    /// Cellar label (what is written on the tape) with the internal code, e.g. "F.2 (F-1)".
    public static string Vessel(string? code, string? label) => code == null ? "" : string.IsNullOrWhiteSpace(label) ? code : $"{label} ({code})";
}

/// GET /api/selftest — acceptance checks of sections 5 and 6, run on the exact dataset rows with the default (DEMO) norms.
public static class SelfTest
{
    public record Item(string Group, string Name, string Expected, string Actual, bool Pass);

    static LossEngine.Op ToOp(Seed.OpRow o, long id) => new()
    {
        Id = id, Date = o.Date, Type = o.Type, FromCode = o.From, ToCode = o.To, Litres = o.Litres, Bottles = o.Bottles, Ml = o.Ml, Remainder = o.Remainder,
    };

    public static object Run(string? baseUrl = null, string? webRoot = null)
    {
        var items = new List<Item>();
        void Add(string g, string n, string exp, string act, bool ok) => items.Add(new(g, n, exp, act, ok));
        static bool Near(double? a, double b, double tol) => a != null && Math.Abs(a.Value - b) <= tol;

        // ---- Section 5: calibration ----
        var q = Calibration.Parse(Calibration.QvevriJson());
        Add("calibration", "Qvevri raw total (before scaling) ≈ 1,490 L", "≈ 1490", $"{Calibration.QvevriRawTotal():0.0}", Near(Calibration.QvevriRawTotal(), 1490, 15));
        Add("calibration", "Qvevri full volume = 1,500 L ± 1", "1500 ± 1", $"{q.Capacity:0.00}", Near(q.Capacity, 1500, 1));
        var t = Calibration.CylinderLitres(220, 290);
        Add("calibration", "Ø220×290 tank capacity ≈ 11,024 L", "≈ 11024", $"{t:0.0}", Near(t, 11024, 1));
        var t3 = Calibration.Parse(Calibration.CylinderJson(110, 160));
        var dip = t3.DipForLitres(1188);
        Add("calibration", "T3-x cylinder: 1,188 L ↔ dip ≈ 35.0 cm", "35.0", $"{dip:0.00}", Math.Round(dip, 1) == 35.0);
        Add("calibration", "T3-x cylinder: dip 35.0 cm → ≈ 1,188 L", "≈ 1188", $"{t3.LitresAtDip(35.0):0.0}", Near(t3.LitresAtDip(35.0), 1188, 1));

        // ---- Section 6: loss engine ----
        var norms = LossEngine.Norms(Db.DefaultSettings);
        const double vpl = 2.99, lpt = 631;
        var w01 = LossEngine.Replay(Seed.W01Ops.Select((o, i) => ToOp(o, i + 1)).ToList(), norms, vpl);
        var w03 = LossEngine.Replay(Seed.W03Ops.Select((o, i) => ToOp(o, i + 1)).ToList(), norms, vpl);

        void Stage(string g, LossEngine.Result r, string key, int occurrence, double lossL, double? pct, string status, double? excess = null)
        {
            var s = r.Stages.Where(x => x.StageKey == key).ElementAtOrDefault(occurrence);
            var exp = $"{lossL} L" + (pct != null ? $" ({pct:0.00}%)" : "") + $" {status}" + (excess != null ? $" excess {excess:0.0} L" : "");
            var act = s == null ? "missing" : $"{s.LossL} L ({s.LossPct:0.00}%) {s.Status}" + (excess != null ? $" excess {s.ExcessL:0.0} L" : "");
            bool ok = s != null && Near(s.LossL, lossL, 0.01) && (pct == null || Math.Round(s.LossPct ?? -1, 2) == pct) && s.Status == status
                      && (excess == null || Math.Round(s.ExcessL ?? -1, 1) == excess);
            Add(g, $"{key}{(occurrence > 0 ? " #" + (occurrence + 1) : "")}", exp, act, ok);
        }

        Stage("W01", w01, "racking_first", 0, 280, 2.77, "ok");
        Stage("W01", w01, "stock_check", 0, 0, 0.00, "ok");
        Stage("W01", w01, "racking", 0, 100, 1.02, "ok");
        Stage("W01", w01, "filtration", 0, 50, 0.51, "ok");
        Stage("W01", w01, "bottling", 0, 20, 0.21, "ok");
        Add("W01", "remainder", "20 L", $"{w01.RemainderL} L", Near(w01.RemainderL, 20, 0.01));
        Add("W01", "total loss", "450 L", $"{w01.TotalLossL} L", Near(w01.TotalLossL, 450, 0.01));
        Add("W01", "total difference", "470 L", $"{w01.TotalDifferenceL} L", Near(w01.TotalDifferenceL, 470, 0.01));
        Add("W01", "bottle check 12,840 × 0.75 = 9,630", "pass", w01.Checks.First(c => c.Key == "bottle").Status, w01.Checks.First(c => c.Key == "bottle").Status == "pass" && Near(w01.BottledL, 9630, 0.01));
        Add("W01", "vessel never gives out more than it holds", "pass", w01.Checks.First(c => c.Key == "give_out").Status, w01.Checks.First(c => c.Key == "give_out").Status == "pass");
        var originSum = Seed.W01Farmers.Sum(f => f.Kg);
        Add("W01", "origin completeness 16,000 = 5,200+4,300+3,800+2,700", "16000", $"{originSum}", Near(originSum, 16000, 0.01));

        Stage("W03", w03, "racking_first", 0, 30, 2.44, "ok");
        Stage("W03", w03, "stock_check", 0, 0, 0.00, "ok");
        Stage("W03", w03, "filtration", 0, 12, 1.00, "above", 4.8);
        Stage("W03", w03, "bottling", 0, 6, 0.51, "above", 2.4);
        Add("W03", "total loss", "48 L", $"{w03.TotalLossL} L", Near(w03.TotalLossL, 48, 0.01));
        Add("W03", "bottle check 1,576 × 0.75 = 1,182", "pass", w03.Checks.First(c => c.Key == "bottle").Status, w03.Checks.First(c => c.Key == "bottle").Status == "pass" && Near(w03.BottledL, 1182, 0.01));

        Add("money", "W01 batch loss 450 L × $2.99", "$1,345.50", $"${w01.LossValueUsd:N2}", Near(w01.LossValueUsd, 1345.50, 0.001));
        var w03ex = w03.Stages.First(s => s.StageKey == "filtration").ExcessL ?? 0;
        Add("money", "W03 filtration excess 4.8 L × $2.99", "$14.35", $"${Math.Round(w03ex, 1) * vpl:N2}", Math.Round(Math.Round(w03ex, 1) * vpl, 2) == 14.35);
        var expected = new Dictionary<string, double> { ["W04"] = 1509, ["W03"] = 1887, ["W02"] = 5660, ["W05"] = 9433, ["W01"] = 37734 };
        foreach (var w in Seed.Wineries.OrderBy(w => w.YearlyT))
        {
            var v = Math.Round(w.YearlyT * lpt * 0.01 * vpl, 0, MidpointRounding.AwayFromZero);
            Add("money", $"1% of loss per year {w.Code} ({w.YearlyT} t)", $"${expected[w.Code]:N0}", $"${v:N0}", v == expected[w.Code]);
        }

        // DB seed matches the dataset rows
        try
        {
            using var c = Db.Open();
            var s = new Dictionary<string, string>(Db.DefaultSettings);
            foreach (var (code, loss) in new[] { ("W01-B1-2025", 450.0), ("W03-B3-2025", 48.0) })
            {
                var id = Db.Query(c, "SELECT id FROM batches WHERE code=$1", code).FirstOrDefault()?["id"];
                if (id == null) { Add("database", $"{code} seeded", "present", "missing", false); continue; }
                var r = LossEngine.Analyze(c, (long)id, s, compare: false, norms: norms);
                Add("database", $"{code} replay from DB: total loss", $"{loss} L", $"{r.TotalLossL} L", Near(r.TotalLossL, loss, 0.01));
                foreach (var ch in r.Checks.Where(x => x.Key is "origin" or "pdo"))
                    Add("database", $"{code} {ch.Label}", "pass", ch.Status, ch.Status == "pass");
            }
        }
        catch (Exception e) { Add("database", "open database", "ok", e.Message, false); }

        // ---- Update 2: fermentation readings ----
        var pa = Readings.PotentialAbv(22.0);
        Add("readings", "potential_abv(22.0) = 12.98 ± 0.01", "12.98", $"{pa:0.00}", Near(pa, 12.98, 0.01));
        var sg = Readings.BrixToSg(22.0);
        Add("readings", "brix_to_sg(22.0) ≈ 1.0920 ± 0.001", "1.0920", $"{sg:0.0000}", Near(sg, 1.0920, 0.001));
        try
        {
            using var c = Db.Open();
            long Count(string batch, string type, string source) => (long)Db.Query(c,
                "SELECT COUNT(*) n FROM readings r JOIN batches b ON b.id=r.batch_id WHERE b.code=$1 AND r.type=$2 AND r.source=$3", batch, type, source)[0]["n"]!;
            foreach (var (batch, type, source, n) in new[]
            {
                ("W01-B1-2025", "temperature", "Dataset A3", 14L),
                ("W03-B3-2025", "temperature", "Dataset A4.2", 5L), ("W03-B3-2025", "ph", "Dataset A4.2", 5L), ("W03-B3-2025", "ta", "Dataset A4.2", 5L),
                ("W02-B2-2025", "ph", "Dataset A4.1", 7L), ("W02-B2-2025", "ta", "Dataset A4.1", 7L),
            })
            {
                var got = Count(batch, type, source);
                Add("readings", $"{batch}: {n} {type} readings from {source}", $"{n}", $"{got}", got == n);
            }
            var seedCodes = Seed.Batches.Select(b => b.Code).ToArray();
            var invented = (long)Db.Query(c, $"""
                SELECT COUNT(*) n FROM readings r JOIN batches b ON b.id=r.batch_id
                WHERE r.type IN ('initial_brix','hydrometer_density','hydrometer_abv')
                  AND (b.code IN ({string.Join(",", seedCodes.Select(x => $"'{x}'"))}) OR r.source NOT LIKE 'Entered%')
                """)[0]["n"]!;
            Add("readings", "No seeded batch has initial_brix / hydrometer readings (nothing invented)", "0", $"{invented}", invented == 0);
        }
        catch (Exception e) { Add("readings", "read readings table", "ok", e.Message, false); }

        // Probes run in a transaction that is always rolled back — the database is not changed.
        try
        {
            var (late, early) = Db.Probe((c, tx) =>
            {
                var b = Seed.BatchId(c, tx, "W01-B1-2025");
                var f1 = Seed.VesselId(c, "W01", "F-1");
                var (_, w1) = Readings.Insert(c, tx, new Readings.Input(b, f1, "2025-09-25T10:00", "initial_brix", 22.0, null, null, "selftest probe", "Entered"));
                var w0 = Readings.Warnings(c, new Readings.Input(b, f1, "2025-09-17T08:00", "initial_brix", 22.0, null, null, null, "Entered"));
                return (w1, w0);
            });
            Add("readings", "Saving initial_brix after the first fermentation reading of the vessel returns a warning", "warning",
                late.Count > 0 ? late[0] : "no warning", late.Any(w => w.StartsWith("Fermentation has already started")));
            Add("readings", "initial_brix before the first fermentation reading: no fermentation warning", "no fermentation warning",
                early.Any(w => w.StartsWith("Fermentation has already started")) ? "warning" : "no fermentation warning", !early.Any(w => w.StartsWith("Fermentation has already started")));

            string Blocked(string sql) => Db.Probe((c, tx) =>
            {
                try { Db.Exec(c, tx, sql); return "allowed"; }
                catch (Microsoft.Data.Sqlite.SqliteException e) { return e.Message.Contains("append-only") ? "blocked" : e.Message; }
            });
            foreach (var (name, sql) in new[]
            {
                ("readings: UPDATE blocked", "UPDATE readings SET value=value+1 WHERE id=(SELECT MIN(id) FROM readings)"),
                ("readings: DELETE blocked", "DELETE FROM readings WHERE id=(SELECT MIN(id) FROM readings)"),
                ("operations: UPDATE blocked", "UPDATE operations SET litres=0 WHERE id=(SELECT MIN(id) FROM operations)"),
                ("operations: DELETE blocked", "DELETE FROM operations WHERE id=(SELECT MIN(id) FROM operations)"),
            })
            {
                var r = Blocked(sql);
                Add("append-only", name, "blocked", r, r == "blocked");
            }
        }
        catch (Exception e) { Add("readings", "probe", "ok", e.Message, false); }

        LiveChecks(Add, baseUrl);
        Assistant.SelfChecks(Add, baseUrl, webRoot);
        RangeSelfTest.Run(Add);
        LedgerSelfTest.Run(Add);
        SealSelfTest.Run(Add);

        return new { pass = items.All(i => i.Pass), passed = items.Count(i => i.Pass), total = items.Count, note = "Loss checks use the dataset rows and the default DEMO ASSUMPTION norms. Reading and live probes run in a rolled-back transaction. Assistant checks never call an AI provider.", checks = items };
    }

    /// Everything the record (journals, batch loss totals, ROI) shows — must be identical with Live ON and OFF.
    static string RecordSnapshot(Microsoft.Data.Sqlite.SqliteConnection c)
    {
        var s = new Dictionary<string, string>(Db.DefaultSettings);
        var parts = new List<object>();
        for (int n = 1; n <= 8; n++) parts.Add(Journals.Build(c, n, null, null).Rows);
        foreach (var b in Db.Query(c, "SELECT id FROM batches ORDER BY id"))
        {
            var r = LossEngine.Analyze(c, (long)b["id"]!, s, compare: false);
            parts.Add(new { r.BatchCode, r.TotalLossL, r.LossValueUsd, r.BottledL, r.LitresNow, r.TotalDifferenceL });
        }
        parts.Add(Money.OnePercentTable(c, 2.99, 631));
        foreach (var t in new[] { "operations", "readings", "lab", "deliveries", "orders", "batches", "batch_origins", "vessels" })
            parts.Add(Db.Query(c, $"SELECT COUNT(*) n FROM {t}")[0]["n"]!);
        return System.Text.Json.JsonSerializer.Serialize(parts);
    }

    static void LiveChecks(Action<string, string, string, string, bool> Add, string? baseUrl)
    {
        try
        {
            var (same, events, phases) = Db.Probe((c, tx) =>
            {
                var before = RecordSnapshot(c);
                var sim = new LiveSim(seed: 42) { On = true };
                sim.Start(c, new Dictionary<string, string>(Db.DefaultSettings));
                var all = new List<LiveEvent>();
                var notes = new Dictionary<string, List<LiveEvent>>();
                foreach (var (scenario, ticks) in new[] { ("hot", 12), ("loss", 5), ("stuck", 12), ("missing", 2), ("allgood", 1) })
                {
                    sim.SetScenario(c, scenario);
                    var ev = new List<LiveEvent>();
                    for (int i = 0; i < ticks; i++) ev.AddRange(sim.Tick(c, tx));
                    all.AddRange(ev);
                    notes[scenario] = ev.Where(e => e.Kind == "notification").ToList();
                }
                var after = RecordSnapshot(c);
                var stored = (long)Db.Query(c, "SELECT COUNT(*) n FROM live_events WHERE source='SIMULATED'")[0]["n"]!;
                return (before == after && stored >= all.Count, all, notes);
            });
            Add("live", "Live events only in live_events: journals, batch loss totals and ROI identical with Live ON and OFF", "identical", same ? "identical" : "changed", same);
            Add("live", "Every simulated live event has source = 'SIMULATED'", $"{events.Count} SIMULATED", $"{events.Count(e => e.Source == "SIMULATED")} SIMULATED", events.Count > 0 && events.All(e => e.Source == "SIMULATED"));
            var other = (long)Db.Query("SELECT COUNT(*) n FROM live_events WHERE source NOT IN ('SIMULATED','SENSOR')")[0]["n"]!;
            Add("live", "Stored live events: none without a SIMULATED (or SENSOR stub) tag", "0", $"{other}", other == 0);
            foreach (var (scenario, level, rule) in new[] { ("hot", "red", "temp_high"), ("loss", "red", "level_drop"), ("stuck", "amber", "stuck"), ("missing", "amber", "missing_brix"), ("allgood", "green", "") })
            {
                var hit = phases[scenario].FirstOrDefault(e => e.Level == level && (rule == "" || e.Rule == rule));
                Add("live", $"Scenario \"{LiveSim.ScenarioTitles[scenario]}\" → {level} {(rule == "" ? "back to normal" : rule)}", $"{level} {rule}".Trim(),
                    hit?.Message ?? string.Join(" | ", phases[scenario].Select(e => $"{e.Level} {e.Rule}")), hit != null);
            }
        }
        catch (Exception e) { Add("live", "live simulator probe", "ok", e.Message, false); }

        // SSE endpoint content type
        if (baseUrl != null)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                using var resp = http.Send(new HttpRequestMessage(HttpMethod.Get, baseUrl + "/api/live/stream?once=true"), HttpCompletionOption.ResponseHeadersRead);
                var ctype = resp.Content.Headers.ContentType?.MediaType ?? "none";
                Add("live", "SSE endpoint /api/live/stream responds with text/event-stream", "text/event-stream", ctype, ctype == "text/event-stream");
            }
            catch (Exception e) { Add("live", "SSE endpoint /api/live/stream responds with text/event-stream", "text/event-stream", e.Message, false); }
        }
        else Add("live", "SSE endpoint /api/live/stream responds with text/event-stream", "text/event-stream", "not checked (no server URL)", false);
    }
}
