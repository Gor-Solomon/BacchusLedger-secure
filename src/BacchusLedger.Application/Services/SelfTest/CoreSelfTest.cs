using BacchusLedger.Domain.Dataset;
using BacchusLedger.Application.Extensions;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Domain.Vessels;
using BacchusLedger.Domain.Losses;
using BacchusLedger.Domain.Readings;

namespace BacchusLedger.Application.Services.SelfTest;

/// Sections 5 and 6 (calibration, loss engine, money) on the exact dataset rows with the default (DEMO) norms,
/// the seeded database, and the fermentation readings checks (probes run in a rolled-back transaction).
public sealed class CoreSelfTest(
    IBatchRepository batches, IReadingRepository readings, IVesselRepository vessels, IDiagnosticsRepository diagnostics, IUnitOfWork unitOfWork,
    ILossAnalysisService losses, IReadingService readingService) : ISelfTestSection
{
    static CellarOperation ToOp(SeedOperation o, long id) => new()
    {
        Id = id, Date = o.Date, Type = o.Type, FromCode = o.From, ToCode = o.To, Litres = o.Litres, Bottles = o.Bottles, Ml = o.Ml, Remainder = o.Remainder,
    };

    public async Task RunAsync(SelfTestContext ctx)
    {
        var Add = ctx.Add;
        static bool Near(double? a, double b, double tol) => SelfTestContext.Near(a, b, tol);

        // Section 5: calibration
        var q = VesselCalibration.Parse(new QvevriProfile().ToCalibrationJson());
        Add("calibration", "Qvevri raw total (before scaling) ≈ 1,490 L", "≈ 1490", $"{new QvevriProfile().RawTotalLitres():0.0}", Near(new QvevriProfile().RawTotalLitres(), 1490, 15));
        Add("calibration", "Qvevri full volume = 1,500 L ± 1", "1500 ± 1", $"{q.Capacity:0.00}", Near(q.Capacity, 1500, 1));
        var t = new CylinderTank(220, 290).Capacity;
        Add("calibration", "Ø220×290 tank capacity ≈ 11,024 L", "≈ 11024", $"{t:0.0}", Near(t, 11024, 1));
        var t3 = VesselCalibration.Parse(new CylinderTank(110, 160).ToCalibrationJson());
        var dip = t3.DipForLitres(1188);
        Add("calibration", "T3-x cylinder: 1,188 L ↔ dip ≈ 35.0 cm", "35.0", $"{dip:0.00}", Math.Round(dip, 1) == 35.0);
        Add("calibration", "T3-x cylinder: dip 35.0 cm → ≈ 1,188 L", "≈ 1188", $"{t3.LitresAtDip(35.0):0.0}", Near(t3.LitresAtDip(35.0), 1188, 1));

        // Section 6: loss engine
        var norms = SettingDefaults.Values.Norms();
        const double vpl = 2.99, lpt = 631;
        var w01 = new LossCalculator(norms, vpl).Replay(SeedDataset.W01Ops.Select((o, i) => ToOp(o, i + 1)).ToList());
        var w03 = new LossCalculator(norms, vpl).Replay(SeedDataset.W03Ops.Select((o, i) => ToOp(o, i + 1)).ToList());

        void Stage(string g, LossResult r, string key, int occurrence, double lossL, double? pct, string status, double? excess = null)
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
        var originSum = SeedDataset.W01Farmers.Sum(f => f.Kg);
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
        foreach (var w in SeedDataset.Wineries.OrderBy(w => w.YearlyT))
        {
            var v = Math.Round(w.YearlyT * lpt * 0.01 * vpl, 0, MidpointRounding.AwayFromZero);
            Add("money", $"1% of loss per year {w.Code} ({w.YearlyT} t)", $"${expected[w.Code]:N0}", $"${v:N0}", v == expected[w.Code]);
        }

        // DB seed matches the dataset rows
        try
        {
            var s = SettingDefaults.Copy();
            foreach (var (code, loss) in new[] { (SeedDataset.W01Batch, 450.0), (SeedDataset.W03Batch, 48.0) })
            {
                if (await batches.FindIdAsync(code) is not { } id) { Add("database", $"{code} seeded", "present", "missing", false); continue; }
                var r = await losses.AnalyzeAsync(id, s, compare: false, norms: norms);
                Add("database", $"{code} replay from DB: total loss", $"{loss} L", $"{r.TotalLossL} L", Near(r.TotalLossL, loss, 0.01));
                foreach (var ch in r.Checks.Where(x => x.Key is "origin" or "pdo"))
                    Add("database", $"{code} {ch.Label}", "pass", ch.Status, ch.Status == "pass");
            }
        }
        catch (Exception e) { Add("database", "open database", "ok", e.Message, false); }

        // Fermentation readings
        var pa = new InitialBrix(22.0).PotentialAbv;
        Add("readings", "potential_abv(22.0) = 12.98 ± 0.01", "12.98", $"{pa:0.00}", Near(pa, 12.98, 0.01));
        var sg = new InitialBrix(22.0).SpecificGravity;
        Add("readings", "brix_to_sg(22.0) ≈ 1.0920 ± 0.001", "1.0920", $"{sg:0.0000}", Near(sg, 1.0920, 0.001));
        try
        {
            foreach (var (batch, type, source, n) in new[]
            {
                (SeedDataset.W01Batch, "temperature", "Dataset A3", 14L),
                (SeedDataset.W03Batch, "temperature", "Dataset A4.2", 5L), (SeedDataset.W03Batch, "ph", "Dataset A4.2", 5L), (SeedDataset.W03Batch, "ta", "Dataset A4.2", 5L),
                ("W02-B2-2025", "ph", "Dataset A4.1", 7L), ("W02-B2-2025", "ta", "Dataset A4.1", 7L),
            })
            {
                var got = await readings.CountAsync(batch, type, source);
                Add("readings", $"{batch}: {n} {type} readings from {source}", $"{n}", $"{got}", got == n);
            }
            var invented = await readings.CountInventedAsync(SeedDataset.Batches.Select(b => b.Code).ToList());
            Add("readings", "No seeded batch has initial_brix / hydrometer readings (nothing invented)", "0", $"{invented}", invented == 0);
        }
        catch (Exception e) { Add("readings", "read readings table", "ok", e.Message, false); }

        // Probes run in a transaction that is always rolled back: the database is not changed.
        try
        {
            var (late, early) = await unitOfWork.ProbeAsync(async () =>
            {
                var b = await batches.FindIdAsync(SeedDataset.W01Batch) ?? 0;
                var f1 = await vessels.GetIdAsync("W01", "F-1");
                var (_, w1) = await readingService.InsertAsync(new ReadingInput(b, f1, "2025-09-25T10:00", "initial_brix", 22.0, null, null, "selftest probe", "Entered"));
                var w0 = await readingService.WarningsAsync(new ReadingInput(b, f1, "2025-09-17T08:00", "initial_brix", 22.0, null, null, null, "Entered"));
                return (w1, w0);
            });
            Add("readings", "Saving initial_brix after the first fermentation reading of the vessel returns a warning", "warning",
                late.Count > 0 ? late[0] : "no warning", late.Any(w => w.StartsWith("Fermentation has already started")));
            Add("readings", "initial_brix before the first fermentation reading: no fermentation warning", "no fermentation warning",
                early.Any(w => w.StartsWith("Fermentation has already started")) ? "warning" : "no fermentation warning", !early.Any(w => w.StartsWith("Fermentation has already started")));

            foreach (var (name, table, delete) in new[]
            {
                ("readings: UPDATE blocked", "readings", false), ("readings: DELETE blocked", "readings", true),
                ("operations: UPDATE blocked", "operations", false), ("operations: DELETE blocked", "operations", true),
            })
            {
                var r = await unitOfWork.ProbeAsync(() => diagnostics.TryChangeAppendOnlyAsync(table, delete));
                Add("append-only", name, "blocked", r, r == "blocked");
            }
        }
        catch (Exception e) { Add("readings", "probe", "ok", e.Message, false); }
    }
}
