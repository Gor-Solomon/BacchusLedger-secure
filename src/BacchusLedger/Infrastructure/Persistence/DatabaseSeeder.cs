using BacchusLedger.Dataset;
using BacchusLedger.Models;
using BacchusLedger.Repositories;
using BacchusLedger.Services;

namespace BacchusLedger.Infrastructure.Persistence;

/// Section 4 seed data: wineries, vessels, batches, demo farmers, the dataset operations and lab rows. Runs inside the caller's transaction.
public sealed class DatabaseSeeder(
    ISettingsRepository settings, IWineryRepository wineries, IVesselRepository vessels, IOriginRepository origins, ILabRepository lab,
    IDatasetWriter dataset)
{
    public async Task SeedAsync()
    {
        await settings.SaveAsync(SettingDefaults.Values.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value)));

        int i = 1;
        foreach (var w in SeedDataset.Wineries) await wineries.InsertAsync(w.Code, $"Winery {i++}", w.Size, w.YearlyT, w.Qvevri, w.Level);

        // Vessels (section 4.8): tank sizes DEMO ASSUMPTION, qvevri shape Dataset A5
        Task Tank(string w, string code, string name, double d, double h) => vessels.InsertAsync(new NewVessel(w, code, "tank", name,
            Calibration.CylinderJson(d, h), Math.Round(Calibration.CylinderLitres(d, h), 1), "DEMO ASSUMPTION"));
        var qj = Calibration.QvevriJson();
        var qcap = Math.Round(Calibration.Parse(qj).Capacity, 1);
        Task Qvevri(string w, string code) => vessels.InsertAsync(new NewVessel(w, code, "qvevri", $"Qvevri {code}", qj, qcap, "Dataset A5"));

        await Tank("W01", "F-1", "Fermentation tank F-1", 230, 290);
        await Tank("W01", "F-2", "Fermentation tank F-2", 230, 290);
        for (int t = 1; t <= 5; t++) await Tank("W01", $"T1-{t}", $"Tank T1-{t}", 220, 290);
        await Tank("W01", "BT-1", "Bottling tank BT-1", 210, 290);
        await Qvevri("W03", "Q-3");
        await Qvevri("W03", "Q-4");
        for (int t = 1; t <= 3; t++) await Tank("W03", $"T3-{t}", $"Tank T3-{t}", 110, 160);

        // Batches (Dataset A2)
        var ids = new Dictionary<string, long>();
        foreach (var b in SeedDataset.Batches)
            ids[b.Code] = await dataset.InsertBatchAsync(b, b.Code, b.Code is SeedDataset.W01Batch or SeedDataset.W03Batch ? "bottled" : "intake recorded", "Dataset A2");

        // Farmers/plots/orders/deliveries: DEMO ASSUMPTION (the dataset has no farmers)
        var w01 = ids[SeedDataset.W01Batch];
        foreach (var f in SeedDataset.W01Farmers)
        {
            var (fid, pid) = await dataset.EnsureFarmerPlotAsync(f);
            var oid = await dataset.InsertOrderAsync(f, fid, pid, "2025-09-18", "DEMO ASSUMPTION");
            var did = await dataset.InsertDeliveryAsync(f, oid, fid, pid, "2025-09-18", 233.0, "DEMO ASSUMPTION");
            await origins.InsertBatchOriginAsync(w01, did, f.Kg);
        }
        var w03 = ids[SeedDataset.W03Batch];
        {
            var f = SeedDataset.W03Farmer;
            var (fid, pid) = await dataset.EnsureFarmerPlotAsync(f);
            var oid = await dataset.InsertOrderAsync(f, fid, pid, "2025-09-19", "DEMO ASSUMPTION");
            var did = await dataset.InsertDeliveryAsync(f, oid, fid, pid, "2025-09-19", null, "DEMO ASSUMPTION");
            await origins.InsertBatchOriginAsync(w03, did, f.Kg);
        }

        // Operations
        foreach (var o in SeedDataset.W01Ops) await dataset.InsertOperationAsync(w01, "W01", o, "Dataset A6.2");
        foreach (var o in SeedDataset.W03Ops) await dataset.InsertOperationAsync(w03, "W03", o, "Dataset A6.1");
        foreach (var b in SeedDataset.Batches.Where(b => b.Code is not (SeedDataset.W01Batch or SeedDataset.W03Batch)))
            await dataset.InsertOperationAsync(ids[b.Code], b.Winery, new SeedOperation(b.Date, "intake", null, null, null, Note: $"{b.Kg:N0} kg grapes (vessel not given in the dataset)"), "Dataset A2");

        // Lab
        for (int d = 0; d < SeedDataset.A3Sugar.Length; d++) await dataset.InsertA3Async(w01, d);
        foreach (var r in SeedDataset.A41) await dataset.InsertA41Async(ids["W02-B2-2025"], r);
        foreach (var r in SeedDataset.A42) await dataset.InsertA42Async(w03, r);
    }

    /// For databases created before readings existed: derive the readings from the dataset lab rows already stored
    /// (seeded batches and any demo replay copies), the same values, nothing new.
    public async Task SeedReadingsFromLabAsync()
    {
        foreach (var l in await lab.ListDatasetRowsAsync())
        {
            var note = l.Source == "Dataset A4.2" ? l.Stage : l.Note;
            if (l.Source != "Dataset A4.1" && l.TempC is { } tc) await dataset.InsertDatasetReadingAsync(l.BatchId, l.VesselId, l.Date, "temperature", tc, l.Source, note);
            if (l.Source != "Dataset A3" && l.Ph is { } ph) await dataset.InsertDatasetReadingAsync(l.BatchId, l.VesselId, l.Date, "ph", ph, l.Source, note);
            if (l.Source != "Dataset A3" && l.TaGl is { } ta) await dataset.InsertDatasetReadingAsync(l.BatchId, l.VesselId, l.Date, "ta", ta, l.Source, note);
        }
    }
}
