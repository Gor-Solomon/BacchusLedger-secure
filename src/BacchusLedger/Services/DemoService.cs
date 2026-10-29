using BacchusLedger.Contracts.Demo;
using BacchusLedger.Dataset;
using BacchusLedger.Extensions;
using BacchusLedger.Models;
using BacchusLedger.Repositories;
using BacchusLedger.Domain.Vessels;
using BacchusLedger.Domain.Losses;
using BacchusLedger.Domain.Common;

namespace BacchusLedger.Services;

/// The guided A→Z replays (W01 classic, W03 qvevri). Each step really writes its data into the database.
public sealed class DemoService(
    IDemoRepository demo, IBatchRepository batches, IVesselRepository vessels, IOriginRepository origins, IOperationRepository operations,
    ILabRepository lab, IReadingRepository readings, IWineryRepository wineries, ISettingsRepository settings, IMaintenanceRepository maintenance,
    IDatasetWriter dataset, ISealService seals, ILossAnalysisService losses, IUnitOfWork unitOfWork) : IDemoService
{
    public const string W01Code = "W01-B1-2025-DEMO", W03Code = "W03-B3-2025-DEMO";

    sealed record StepDef(string Title, string? Date, int[] Journals, Func<Task>? Apply);

    static string N0(double v) => v.ToString("N0");
    static string P2(double? v) => (v ?? 0).ToString("0.00");
    static string Usd(double v) => "$" + v.ToString("N2");
    static string Status(LossStage? s) => s?.Status switch { "ok" => "pass", "above" => "above", "negative" => "fail", _ => "info" };
    static string Script(string? s) => s == "w03" ? "w03" : "w01";
    static string BatchCode(string script) => script == "w03" ? W03Code : W01Code;

    public async Task<DemoStepsDto> StepsAsync(string? script)
    {
        var sc = Script(script);
        var defs = Defs(sc);
        var done = await demo.DoneStepsAsync(sc);
        return new DemoStepsDto(sc, BatchCode(sc), defs.Length,
            defs.Select((d, i) => new DemoStepDto(i, d.Title, d.Date == null ? null : d.Date.ToDisplayDate(), d.Journals, done.Contains(i))).ToList());
    }

    public async Task<ServiceResult<DemoCardDto>> StepAsync(int n, string? script)
    {
        var sc = Script(script);
        var defs = Defs(sc);
        if (n < 0 || n >= defs.Length) return ServiceResult<DemoCardDto>.NotFound("no such step");
        await unitOfWork.WriteAsync(async () => { await ApplyAsync(sc, n); return 0; });
        return await CardAsync(sc, n);
    }

    public async Task<DemoStepsDto> ResetAsync(string? script)
    {
        var sc = Script(script);
        await unitOfWork.WriteAsync(async () =>
        {
            await demo.EnsureTablesAsync();
            var b = await DemoBatchAsync(sc);
            if (b != 0)
                await maintenance.AllowDeleteAsync(async () =>
                {
                    await origins.DeleteBatchOriginsAsync(b);
                    await operations.DeleteForBatchAsync(b);
                    await readings.DeleteForBatchAsync(b);
                    await lab.DeleteForBatchAsync(b);
                });
            foreach (var d in await demo.TrackedAsync(sc, "deliveries")) await origins.DeleteDeliveryAsync(d);
            foreach (var o in await demo.TrackedAsync(sc, "orders")) await origins.DeleteOrderAsync(o);
            if (b != 0)
            {
                await batches.DeleteAsync(b);
                // The replay re-creates rows (often with the same ids), so the old seal of this batch is dropped; the next report seals the new rows.
                await seals.DropAsync(b);
            }
            await demo.ForgetAsync(sc);
            return 0;
        });
        return await StepsAsync(sc);
    }

    public Task ReplayAllAsync(string script) => ApplyAsync(Script(script), Defs(Script(script)).Length - 1);

    public Task<PracticeCopyResponse> CreatePracticeCopyAsync() => unitOfWork.WriteAsync(async () =>
    {
        var src = SeedDataset.Batches.First(x => x.Code == SeedDataset.W03Batch);
        var code = $"{SeedDataset.W03Batch}-TRY-{await batches.CountCodesLikeAsync(SeedDataset.W03Batch + "-TRY-%") + 1}";
        var id = await dataset.InsertBatchAsync(src, code, "in progress (practice copy)", $"Dataset A6.1 — practice copy of {SeedDataset.W03Batch}");
        foreach (var o in await origins.ListOriginLinksAsync(SeedDataset.W03Batch)) await origins.InsertBatchOriginAsync(id, o.DeliveryId, o.Kg);
        foreach (var o in SeedDataset.W03Ops.Take(4)) await dataset.InsertOperationAsync(id, "W03", o, "Dataset A6.1");
        return new PracticeCopyResponse(id, code);
    });

    public Task<LiveDemoStartResponse> StartLiveReadingsAsync(string? vessel) => unitOfWork.WriteAsync(async () =>
    {
        const string code = "W01-LIVE-DEMO";
        var old = await batches.FindIdAsync(code);
        await maintenance.AllowDeleteAsync(async () =>
        {
            if (old != null) await readings.DeleteForBatchAsync(old.Value);
        });
        if (old != null) await batches.DeleteAsync(old.Value);
        await vessels.DeleteUnusedLiveDemoAsync();
        await vessels.ClearLiveDemoLabelsAsync();
        var vcode = "F-2";
        if (vessel == "new")
        {
            vcode = "Q-LIVE";
            if (!await vessels.ExistsAsync("W01", vcode))
                await vessels.InsertAsync(new NewVessel("W01", vcode, "qvevri", "New qvevri (live demo)", null, null, "Entered (live demo)"));
        }
        var id = await batches.InsertAsync(new NewBatch("W01", code, "Live demo batch", "Live demo (values entered by the presenter)", vessel == "new" ? "qvevri" : "classic",
            null, null, null, "fermenting (live demo)", "Entered (live demo) — readings typed during the presentation; live demo copy", null));
        return new LiveDemoStartResponse(id, code, vcode, "W01");
    });

    /// Applies every step up to n that is not written yet (inside the running transaction).
    async Task ApplyAsync(string script, int n)
    {
        await demo.EnsureTablesAsync();
        var defs = Defs(script);
        for (int s = 0; s <= n; s++)
        {
            if (await demo.IsDoneAsync(script, s)) continue;
            if (defs[s].Apply is { } apply) await apply();
            await demo.MarkDoneAsync(script, s);
        }
    }

    // ---------- writing helpers ----------
    async Task<long> DemoBatchAsync(string script) => await batches.FindIdAsync(BatchCode(script)) ?? 0;

    async Task OpAsync(string script, SeedOperation o, string source) =>
        await demo.TrackAsync(script, "operations", await dataset.InsertOperationAsync(await DemoBatchAsync(script), script == "w03" ? "W03" : "W01", o, source));

    async Task OrdersAsync(string script, SeedFarmerPlot[] fs, string date)
    {
        foreach (var f in fs)
        {
            var (fid, pid) = await dataset.EnsureFarmerPlotAsync(f);
            await demo.TrackAsync(script, "orders", await dataset.InsertOrderAsync(f, fid, pid, date, "CBW 2025"));
        }
    }

    async Task DeliveriesAsync(string script, SeedFarmerPlot[] fs, string date, double? sugar)
    {
        var orders = await demo.TrackedAsync(script, "orders");
        for (int i = 0; i < fs.Length; i++)
        {
            var (fid, pid) = await dataset.EnsureFarmerPlotAsync(fs[i]);
            await demo.TrackAsync(script, "deliveries", await dataset.InsertDeliveryAsync(fs[i], orders[i], fid, pid, date, sugar, "DEMO ASSUMPTION"));
        }
    }

    async Task CreateBatchAsync(string script)
    {
        var src = SeedDataset.Batches.First(b => b.Code == (script == "w03" ? SeedDataset.W03Batch : SeedDataset.W01Batch));
        var id = await dataset.InsertBatchAsync(src, BatchCode(script), "in progress (demo replay)", $"Dataset A2 — demo replay copy of {src.Code}");
        await demo.TrackAsync(script, "batches", id);
        foreach (var d in await demo.TrackedAsync(script, "deliveries")) await origins.InsertBatchOriginAsync(id, d, await origins.GetDeliveryKgAsync(d));
    }

    Task SetStatusAsync(string script, string status) => batches.SetStatusAsync(BatchCode(script), status);

    // ---------- step definitions ----------
    StepDef[] Defs(string script) => script == "w03" ? W03Defs() : W01Defs();

    StepDef[] W01Defs() =>
    [
        new("The problem", null, [], null),
        new("Register farmers and plots", null, [1], async () => { foreach (var f in SeedDataset.W01Farmers) await dataset.EnsureFarmerPlotAsync(f); }),
        new("Grape orders", null, [], () => OrdersAsync("w01", SeedDataset.W01Farmers, "2025-09-18")),
        new("Receiving", "2025-09-18", [2], () => DeliveriesAsync("w01", SeedDataset.W01Farmers, "2025-09-18", 233.0)),
        new("Into tank F-1", "2025-09-18", [3, 4], async () => { await CreateBatchAsync("w01"); await OpAsync("w01", SeedDataset.W01Ops[0], "Dataset A6.2"); }),
        new("Fermentation, 14 days", "2025-09-18", [7], async () =>
        {
            var b = await DemoBatchAsync("w01");
            for (int d = 0; d < SeedDataset.A3Sugar.Length; d++) await dataset.InsertA3Async(b, d);
        }),
        new("Separation", "2025-10-02", [5], () => OpAsync("w01", SeedDataset.W01Ops[1], "Dataset A6.2")),
        new("First racking", "2025-10-12", [5], () => OpAsync("w01", SeedDataset.W01Ops[2], "Dataset A6.2")),
        new("Stock check", "2025-12-15", [5], () => OpAsync("w01", SeedDataset.W01Ops[3], "Dataset A6.2")),
        new("Second racking", "2026-03-12", [5], () => OpAsync("w01", SeedDataset.W01Ops[4], "Dataset A6.2")),
        new("Filtration", "2026-04-14", [5], () => OpAsync("w01", SeedDataset.W01Ops[5], "Dataset A6.2")),
        new("Bottling", "2026-04-16", [6], async () => { await OpAsync("w01", SeedDataset.W01Ops[6], "Dataset A6.2"); await SetStatusAsync("w01", "bottled (demo replay)"); }),
        new("Warehouse", "2026-04-17", [6], () => OpAsync("w01", SeedDataset.W01Ops[7], "Dataset A6.2")),
        new("Result", null, [8], null),
        new("What it is worth", null, [], null),
    ];

    StepDef[] W03Defs() =>
    [
        new("Qvevri demo: W03 amber", null, [], null),
        new("Intake into qvevri Q-3", "2025-09-19", [1, 2, 3, 4], async () =>
        {
            await dataset.EnsureFarmerPlotAsync(SeedDataset.W03Farmer);
            await OrdersAsync("w03", [SeedDataset.W03Farmer], "2025-09-19");
            await DeliveriesAsync("w03", [SeedDataset.W03Farmer], "2025-09-19", null);
            await CreateBatchAsync("w03");
            await OpAsync("w03", SeedDataset.W03Ops[0], "Dataset A6.1");
        }),
        new("Lab on pomace", "2025-10-15", [7], async () => await dataset.InsertA42Async(await DemoBatchAsync("w03"), SeedDataset.A42[0])),
        new("Separation from the qvevri", "2025-11-15", [5, 7], async () => { await OpAsync("w03", SeedDataset.W03Ops[1], "Dataset A6.1"); await dataset.InsertA42Async(await DemoBatchAsync("w03"), SeedDataset.A42[1]); }),
        new("Racking", "2025-12-12", [5, 7], async () => { await OpAsync("w03", SeedDataset.W03Ops[2], "Dataset A6.1"); await dataset.InsertA42Async(await DemoBatchAsync("w03"), SeedDataset.A42[2]); }),
        new("Stock check", "2026-03-26", [5, 7], async () => { await OpAsync("w03", SeedDataset.W03Ops[3], "Dataset A6.1"); await dataset.InsertA42Async(await DemoBatchAsync("w03"), SeedDataset.A42[3]); }),
        new("Filtration", "2026-06-10", [5, 7], async () => { await dataset.InsertA42Async(await DemoBatchAsync("w03"), SeedDataset.A42[4]); await OpAsync("w03", SeedDataset.W03Ops[4], "Dataset A6.1"); }),
        new("Bottling", "2026-06-12", [6], async () =>
        {
            await OpAsync("w03", SeedDataset.W03Ops[5], "Dataset A6.1");
            await OpAsync("w03", SeedDataset.W03Ops[6], "Dataset A6.1");
            await SetStatusAsync("w03", "bottled (demo replay)");
        }),
        new("Qvevri rule", null, [], null),
        new("Result", null, [8], null),
    ];

    async Task<DemoCardDto> CardAsync(string script, int n)
    {
        var defs = Defs(script);
        var def = defs[n];
        var s = await settings.GetAsync();
        double vpl = s.Number("value_per_litre_usd"), lpt = s.Number("litres_per_tonne");
        long batchId = await DemoBatchAsync(script);
        var an = batchId != 0 ? await losses.AnalyzeAsync(batchId, s) : null;
        LossStage? St(string date, string type) => an?.Stages.FirstOrDefault(x => x.Date == date && x.Type == type);

        var received = new List<DemoFieldDto>();
        var did = new List<DemoDidDto>();
        var extra = script == "w01" ? await BuildW01Async(n, an, St, vpl, lpt, received, did) : await BuildW03Async(n, an, St, vpl, received, did);

        double kg = 0;
        foreach (var id in await demo.TrackedAsync(script, "deliveries")) kg += await origins.GetDeliveryKgAsync(id) ?? 0;
        var filled = defs.Take(n + 1).SelectMany(d => d.Journals).Distinct().Count();
        var totals = new DemoTotalsDto(kg, an?.LitresNow ?? 0, an?.BottledL ?? 0, an?.TotalLossL ?? 0, an?.LossValueUsd ?? 0, filled, 8);
        return new DemoCardDto(script, n, defs.Length, def.Title, def.Date == null ? null : def.Date.ToDisplayDate(), def.Journals,
            received, did, extra, batchId == 0 ? null : batchId, BatchCode(script), totals);
    }

    async Task<object?> BuildW01Async(int n, LossResult? an, Func<string, string, LossStage?> St, double vpl, double lpt,
        List<DemoFieldDto> rec, List<DemoDidDto> did)
    {
        object? extra = null;
        var fs = SeedDataset.W01Farmers;
        double total = fs.Sum(f => f.Kg);
        switch (n)
        {
            case 0:
                rec.Add(new("Challenge Book 3.1", "“Vineyard and grape intake records are not automatically linked to the wine batch.”", "Challenge Book"));
                rec.Add(new("Challenge Book 3.1", "“Primary records are often moved by hand from paper to Excel.”", "Challenge Book"));
                rec.Add(new("Challenge Book 3.2", "“No winery has a sanitary flow meter.”", "Challenge Book"));
                rec.Add(new("Partner wineries", "5 of 5 partner wineries report it; #2 priority for W02, W03, W05", "Challenge Book"));
                did.Add(new("W01 put 10,100 L in the tank and bottled 9,630 L — 450 L lost + 20 L left in the tank. Which step lost too much? Press Next to replay the batch.", "Dataset A6.2", "info"));
                break;
            case 1:
                foreach (var f in fs) rec.Add(new(f.Farmer, $"{f.Plot} · {f.Ha} ha · {f.Variety} · {SeedDataset.Municipality} · cadastre {f.Cadastre}", "DEMO ASSUMPTION"));
                rec.Add(new("Reference point M1", $"lat {SeedDataset.Lat}, lng {SeedDataset.Lng} · {SeedDataset.Municipality}, {SeedDataset.Region}", "Dataset A1"));
                rec.Add(new("Soil 0–30 cm", $"pH {SeedDataset.SoilPh} · {SeedDataset.SoilTexture}", "Dataset B2"));
                rec.Add(new("Climate Dec 2024–Nov 2025", $"rainfall {SeedDataset.RainMm} mm (99.5+219.4+146.9+138.0) · summer mean {SeedDataset.SummerT} °C", "Dataset A1"));
                did.Add(new("Kakheti PDO zone check: Kvareli is in the §3 list ✓", "Kakheti PDO spec", "pass"));
                did.Add(new("Terroir attached to K-101…K-104 from A1/B2 — regional model data (ERA5, SoilGrids), not an on-site measurement", "Dataset A1", "info"));
                did.Add(new("Journal 1 (Vineyard accounting) updated", "Kakheti PDO spec", null));
                break;
            case 2:
                foreach (var f in fs) rec.Add(new($"Order · {f.Farmer}", $"{N0(f.Kg)} kg {f.Variety}", "DEMO ASSUMPTION"));
                rec.Add(new("Price", "1.50 GEL/kg Saperavi (state purchase price)", "CBW 2025"));
                did.Add(new($"Expected cost = {N0(total)} × 1.50 = {N0(total * SeedDataset.PriceSaperavi)} GEL", "Calculated", null));
                did.Add(new("No journal — orders are a commercial record", "Kakheti PDO spec", "info"));
                break;
            case 3:
                foreach (var f in fs) rec.Add(new($"Delivery · {f.Farmer} · {f.Plot}", $"{N0(f.Kg)} kg", "DEMO ASSUMPTION"));
                rec.Add(new("Batch total", "16,000 kg (the farmer split above is a DEMO split)", "Dataset A2"));
                rec.Add(new("Sugar", "233 g/L (day 1)", "Dataset A3"));
                did.Add(new($"Origin check: 16,000 = {string.Join(" + ", fs.Select(f => N0(f.Kg)))} ✓", "Calculated", Math.Abs(total - 16000) < 0.01 ? "pass" : "fail"));
                did.Add(new($"Potential alcohol ≈ 233 / 17 ≈ {233.0 / 17:0.0}% (rule of thumb ~17 g/L per 1% ABV)", "Calculated", null));
                did.Add(new("Journal 2 (Grape receiving) updated", "Kakheti PDO spec", null));
                break;
            case 4:
                rec.Add(new("Batch", $"{W01Code} · Classic red wine", "Dataset A2"));
                rec.Add(new("Intake", "16,000 kg grapes → F-1, 18.09.2025", "Dataset A6.2"));
                rec.Add(new("Variety", "Saperavi", "DEMO ASSUMPTION"));
                foreach (var f in fs)
                    did.Add(new($"Origin {f.Farmer}: {Share(f.Kg / total * 100)}% · {N0(f.Kg)} / {f.Ha} ha = {N0(f.Kg / f.Ha)} kg/ha", "Calculated", null));
                did.Add(new("Origin check on the batch: " + (an?.Checks.FirstOrDefault(x => x.Key == "origin")?.Detail ?? ""), "Calculated",
                    an?.Checks.FirstOrDefault(x => x.Key == "origin")?.Status));
                did.Add(new("Journals 3 (Grape processing) and 4 (Vintage) updated", "Kakheti PDO spec", null));
                break;
            case 5:
            {
                var sug = SeedDataset.A3Sugar; var tmp = SeedDataset.A3Temp;
                rec.Add(new("Fermentation log F-1", "14 daily rows, 18.09–01.10.2025 (sugar g/L, temperature °C)", "Dataset A3"));
                int fd = 0; double fastest = 0;
                for (int i = 1; i < sug.Length; i++) if (sug[i - 1] - sug[i] > fastest) { fastest = sug[i - 1] - sug[i]; fd = i; }
                int pk = Array.IndexOf(tmp, tmp.Max());
                did.Add(new($"Fastest drop −{fastest:0.#} g/L/day (day {fd}→{fd + 1}: {sug[fd - 1]:0.#} → {sug[fd]:0.#})", "Calculated", null));
                did.Add(new($"Peak temperature {tmp[pk]:0.0} °C on day {pk + 1}", "Calculated", null));
                did.Add(new($"Fermentation finished at {sug[^1]:0.0} g/L", "Dataset A3", "pass"));
                did.Add(new("Journal 7 (Laboratory analysis) updated", "Kakheti PDO spec", null));
                extra = new FermentationChartExtra("fermentation", Enumerable.Range(1, sug.Length).ToList(), Enumerable.Range(0, sug.Length).Select(i => (SeedDataset.A3Date(i)).ToDisplayDate()).ToList(), sug, tmp, "Dataset A3");
                break;
            }
            case 6:
                rec.Add(new("Separation", "F-1 → T1-1 · 10,100 L", "Dataset A6.2"));
                did.Add(new($"Yield = 10,100 / 16 t = {10100 / 16.0:0} L/t", "Calculated", null));
                did.Add(new("Journal 5 (Storage and movement) updated", "Kakheti PDO spec", null));
                break;
            case 7: StageCard(St("2025-10-12", "racking"), "T1-1 → T1-2 · 9,820 L", rec, did, vpl); break;
            case 8: StageCard(St("2025-12-15", "stock_check"), "T1-2 = 9,820 L", rec, did, vpl); break;
            case 9: StageCard(St("2026-03-12", "racking"), "T1-2 → T1-3 · 9,720 L", rec, did, vpl); break;
            case 10:
                StageCard(St("2026-04-14", "filtration"), "T1-3 → BT-1 · 9,670 L", rec, did, vpl);
                var o = St("2026-04-14", "filtration")?.Other;
                if (o != null)
                {
                    did.Insert(did.Count - 1, new($"Comparison: W03 lost {P2(o.LossPct)}% at the same step → flagged at W03", "Dataset A6.1", o.Status == "above" ? "above" : "info"));
                }
                break;
            case 11:
            {
                var b = St("2026-04-16", "bottling");
                rec.Add(new("Bottling", "BT-1 → 12,840 bottles × 750 mL", "Dataset A6.2"));
                rec.Add(new("Recorded litres", "9,630 L · remainder 20 L in BT-1", "Dataset A6.2"));
                var bc = an?.Checks.FirstOrDefault(x => x.Key == "bottle");
                did.Add(new($"Bottle check: 12,840 × 0.75 = {N0(12840 * 0.75)} L {(bc?.Status == "pass" ? "✓" : "✗")}", "Calculated", bc?.Status ?? "info"));
                did.Add(new($"{N0(b?.RemainderL ?? 0)} L remainder stays in BT-1", "Dataset A6.2", "info"));
                if (b != null) did.Add(new($"Loss {N0(b.LossL ?? 0)} L = {P2(b.LossPct)}% (normal ≤ {b.NormPct:0.0#}%) {(b.Status == "ok" ? "✓" : "")}", "Calculated", Status(b)));
                did.Add(new("Journal 6 (Bottling) updated", "Kakheti PDO spec", null));
                break;
            }
            case 12:
                rec.Add(new("Warehouse", "12,840 bottles → finished goods", "Dataset A6.2"));
                did.Add(new("Bottles moved to finished goods; batch closed in the cellar record", "Dataset A6.2", "pass"));
                did.Add(new("Journal 6 (Bottling) updated", "Kakheti PDO spec", null));
                break;
            case 13:
                rec.Add(new("Grapes", "16,000 kg", "Dataset A2"));
                rec.Add(new("After separation", "10,100 L", "Dataset A6.2"));
                rec.Add(new("Bottled", "9,630 L (12,840 bottles)", "Dataset A6.2"));
                if (an != null)
                {
                    did.Add(new($"Total loss {N0(an.TotalLossL)} L (+{N0(an.RemainderL)} L remainder) = {N0(an.TotalDifferenceL ?? 0)} L difference", "Dataset A6.2", null));
                    did.Add(new($"Value {Usd(an.LossValueUsd)} at ${vpl:0.00}/L", "Calculated", null));
                    foreach (var st in an.Stages.Where(x => x.LossL != null))
                        did.Add(new($"{st.StageKey.Replace('_', ' ')}: {N0(st.LossL!.Value)} L ({P2(st.LossPct)}%)", "Calculated", Status(st)));
                }
                did.Add(new("8 of 8 journals available (tasting = template)", "Kakheti PDO spec", "pass"));
                extra = new ButtonsExtra(["history", "journals"]);
                break;
            case 14:
                rec.Add(new("Market", "1,027 wine companies in Georgia in 2023 (6 large, 29 medium, 982 small)", "TBC Capital 2024"));
                rec.Add(new("UVP", "The only cellar record that works with qvevri, in Georgian, for Kakheti law, with no sensors and no internet needed.", "Hackathon slides"));
                did.Add(new("Not proven yet: how much loss a winery can win back — the pilot (Nov–Dec) measures it.", "Calculated", "info"));
                extra = new OnePercentExtra(Money.OnePercentTable(await wineries.ListBySizeAsync(), vpl, lpt));
                break;
        }
        return extra;
    }

    static void StageCard(LossStage? s, string received, List<DemoFieldDto> rec, List<DemoDidDto> did, double vpl)
    {
        rec.Add(new(s?.StageKey == "stock_check" ? "Stock check" : s?.StageKey == "filtration" ? "Filtration" : "Racking", received, s?.Source ?? "Dataset"));
        if (s == null) return;
        if (s.Type == "stock_check")
            did.Add(new($"{N0(s.LossL ?? 0)} L difference {(s.Status == "ok" ? "✓" : "")}", "Calculated", Status(s)));
        else if (s.Status == "above")
            did.Add(new($"Loss {N0(s.LossL ?? 0)} L — ABOVE normal: {P2(s.LossPct)}% vs norm {s.NormPct:0.0#}% (DEMO ASSUMPTION norm), excess {s.ExcessL:0.0} L ≈ {Usd((s.ExcessL ?? 0) * vpl)}", "Calculated", "above"));
        else
            did.Add(new($"Loss {N0(s.LossL ?? 0)} L = {P2(s.LossPct)}%; normal ≤ {s.NormPct:0.0#}% (DEMO ASSUMPTION norm) {(s.Status == "ok" ? "✓" : "— more than recorded, check the reading")}",
                "Calculated", Status(s)));
        if (s.Other != null && s.Type != "filtration")
        {
            var o = s.Other;
            did.Add(new($"{o.Winery} same step: {P2(o.LossPct)}%", o.Source ?? "Dataset", "info"));
        }
        did.Add(new("Journal 5 (Storage and movement) updated", "Kakheti PDO spec", null));
    }

    async Task<object?> BuildW03Async(int n, LossResult? an, Func<string, string, LossStage?> St, double vpl,
        List<DemoFieldDto> rec, List<DemoDidDto> did)
    {
        object? extra = null;
        var f = SeedDataset.W03Farmer;
        switch (n)
        {
            case 0:
                rec.Add(new("Winery W03", "Small to medium · 100 t/year · uses qvevri · readiness L2", "Challenge Book"));
                rec.Add(new("Batch 3", "Qvevri amber wine · 1,850 kg grapes · intake 19.09.2025", "Dataset A2"));
                did.Add(new("Press Next to replay W03 batch 3 from qvevri to bottle (Dataset A6.1).", "Dataset A6.1", "info"));
                break;
            case 1:
                rec.Add(new(f.Farmer, $"{f.Plot} · {f.Ha} ha · {f.Variety} · {SeedDataset.Municipality}", "DEMO ASSUMPTION"));
                rec.Add(new("Intake", "1,850 kg grapes → Q-3, 19.09.2025", "Dataset A6.1"));
                rec.Add(new("Price", "1.20 GEL/kg (other permitted varieties, state purchase price)", "CBW 2025"));
                foreach (var ch in an?.Checks.Where(x => x.Key is "pdo" or "origin") ?? [])
                    did.Add(new($"{ch.Label}: {ch.Detail}", ch.Source, ch.Status));
                did.Add(new($"kg per ha = 1,850 / {f.Ha} = {N0(f.Kg / f.Ha)} kg/ha", "Calculated", null));
                did.Add(new("Journals 1, 2, 3, 4 updated", "Kakheti PDO spec", null));
                break;
            case 2:
            {
                var l = SeedDataset.A42[0];
                rec.Add(new("Lab 15.10.2025 · Q-3 · on pomace", $"{l.Temp} °C · pH {l.Ph:0.00} · TA {l.Ta:0.0} g/L · RS {l.Rs:0.0} g/L", "Dataset A4.2"));
                did.Add(new("Lab row stored against the batch and vessel Q-3", "Dataset A4.2", "pass"));
                did.Add(new("Journal 7 (Laboratory analysis) updated", "Kakheti PDO spec", null));
                break;
            }
            case 3:
            {
                rec.Add(new("Separation", "Q-3 → T3-1 · 1,230 L", "Dataset A6.1"));
                rec.Add(new("Lab 20.11.2025 · T3-1 · storage", "15.2 °C · pH 3.47 · TA 5.9 · RS 2.3", "Dataset A4.2"));
                var m = VesselCalibration.TryParse((await vessels.GetAsync("W03", "Q-3"))?.CalibrationJson) ?? VesselCalibration.Parse(new QvevriProfile().ToCalibrationJson());
                var dip = m.DipForLitres(1230);
                did.Add(new($"Qvevri dip: 1,230 L in Q-3 ↔ dip {dip:0.0} cm from the rim (A5 table)", "Calculated", "info"));
                did.Add(new($"Yield = 1,230 / 1.85 t = {1230 / 1.85:0} L/t", "Calculated", null));
                did.Add(new("Journals 5 and 7 updated", "Kakheti PDO spec", null));
                extra = new QvevriDipExtra(Math.Round(dip, 1), 1230, "Q-3", Math.Round(m.Capacity, 0));
                break;
            }
            case 4:
                StageCard(St("2025-12-12", "racking"), "T3-1 → T3-2 · 1,200 L", rec, did, vpl);
                rec.Add(new("Lab 15.12.2025 · T3-2 · ageing", "12.8 °C · pH 3.47 · TA 5.8 · RS 2.2", "Dataset A4.2"));
                break;
            case 5:
                StageCard(St("2026-03-26", "stock_check"), "T3-2 = 1,200 L", rec, did, vpl);
                rec.Add(new("Lab 26.03.2026 · T3-2 · ageing", "14.0 °C · pH 3.48 · TA 5.8 · RS 2.2", "Dataset A4.2"));
                break;
            case 6:
            {
                var s = St("2026-06-10", "filtration");
                StageCard(s, "T3-2 → T3-3 · 1,188 L", rec, did, vpl);
                rec.Add(new("Lab 09.06.2026 · T3-2 · before bottling", "16.1 °C · pH 3.48 · TA 5.7 · RS 2.2", "Dataset A4.2"));
                if (s != null)
                {
                    if (s.Other is { } o) did.Insert(did.Count - 1, new($"W01 same step: {P2(o.LossPct)}%", "Dataset A6.2", "info"));
                }
                break;
            }
            case 7:
            {
                var b = St("2026-06-12", "bottling");
                rec.Add(new("Bottling", "T3-3 → 1,576 bottles × 750 mL = 1,182 L · remainder 0", "Dataset A6.1"));
                rec.Add(new("Warehouse 13.06.2026", "1,576 bottles → finished goods", "Dataset A6.1"));
                var bc = an?.Checks.FirstOrDefault(x => x.Key == "bottle");
                did.Add(new($"Bottle check: 1,576 × 0.75 = {N0(1576 * 0.75)} L {(bc?.Status == "pass" ? "✓" : "✗")}", "Calculated", bc?.Status ?? "info"));
                if (b != null)
                {
                    did.Add(new($"Loss {N0(b.LossL ?? 0)} L = {P2(b.LossPct)}% vs norm {b.NormPct:0.0#}%{(b.Status == "above" ? $" — ABOVE, excess {b.ExcessL:0.0} L ≈ {Usd((b.ExcessL ?? 0) * vpl)}" : "")}", "Calculated", Status(b)));
                    if (b.Other is { } o) did.Add(new($"W01 same step: {P2(o.LossPct)}%", "Dataset A6.2", "info"));
                }
                did.Add(new("Journal 6 (Bottling) updated", "Kakheti PDO spec", null));
                break;
            }
            case 8:
            {
                var q = an?.Checks.FirstOrDefault(x => x.Key == "qvevri_rule");
                rec.Add(new("Technology", "Qvevri amber wine", "Dataset A2"));
                rec.Add(new("Separated from qvevri", "15.11.2025", "Dataset A6.1"));
                did.Add(new(q?.Detail ?? "", "Challenge Book", "info"));
                break;
            }
            case 9:
                rec.Add(new("Grapes", "1,850 kg", "Dataset A2"));
                rec.Add(new("After separation", "1,230 L", "Dataset A6.1"));
                rec.Add(new("Bottled", "1,182 L (1,576 bottles)", "Dataset A6.1"));
                if (an != null)
                {
                    did.Add(new($"Total loss {N0(an.TotalLossL)} L", "Dataset A6.1", null));
                    did.Add(new($"Value {Usd(an.LossValueUsd)} at ${vpl:0.00}/L", "Calculated", null));
                    did.Add(new($"Above-normal excess {an.ExcessL:0.0} L ≈ {Usd(an.ExcessL * vpl)} (filtration + bottling)", "Calculated", an.ExcessL > 0.5 ? "above" : "pass"));
                    foreach (var st in an.Stages.Where(x => x.LossL != null))
                        did.Add(new($"{st.StageKey.Replace('_', ' ')}: {N0(st.LossL!.Value)} L ({P2(st.LossPct)}%)", "Calculated", Status(st)));
                }
                did.Add(new("8 of 8 journals available (tasting = template)", "Kakheti PDO spec", "pass"));
                extra = new ButtonsExtra(["history", "journals"]);
                break;
        }
        return extra;
    }

    /// 32.5, 26.9, 23.75, 16.9 — two decimals only when exact, otherwise one.
    static string Share(double pct)
    {
        var r2 = Math.Round(pct, 2);
        return Math.Abs(r2 - pct) < 1e-9 ? r2.ToString("0.##") : pct.ToString("0.0");
    }
}
