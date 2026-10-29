using BacchusLedger.Dataset;
using BacchusLedger.Models;
using BacchusLedger.Repositories;
using BacchusLedger.Domain.Readings;

namespace BacchusLedger.Services;

public sealed class DatasetWriter(
    IBatchRepository batches, IVesselRepository vessels, IOriginRepository origins, IOperationRepository operations, ILabRepository lab,
    IReadingRepository readings) : IDatasetWriter
{
    public Task<long> InsertBatchAsync(SeedBatch b, string code, string status, string sourceNote) =>
        batches.InsertAsync(new NewBatch(b.Winery, code, $"{b.WineType} 2025", b.WineType, b.Tech, b.Variety, 2025, b.Abv, status, sourceNote, b.Kg));

    public async Task<(long FarmerId, long PlotId)> EnsureFarmerPlotAsync(SeedFarmerPlot f)
    {
        long fid = await origins.FindFarmerIdAsync(f.FarmerCode) ?? await origins.InsertFarmerAsync(f.FarmerCode, f.Farmer, null, isDemo: true);
        long pid = await origins.FindPlotIdAsync(f.Plot) ?? await origins.InsertPlotAsync(new NewPlot(fid, f.Winery, f.Plot, f.Cadastre, null,
            SeedDataset.Municipality, SeedDataset.Region, f.Ha, f.Variety, SeedDataset.Lat, SeedDataset.Lng, "DEMO", SeedDataset.SoilTexture, SeedDataset.SoilPh,
            SeedDataset.RainMm, SeedDataset.SummerT, 1));
        return (fid, pid);
    }

    public Task<long> InsertOrderAsync(SeedFarmerPlot f, long farmerId, long plotId, string date, string source) =>
        origins.InsertOrderAsync(new NewOrder(f.Winery, farmerId, plotId, f.Variety, f.Kg, f.Variety == "Saperavi" ? SeedDataset.PriceSaperavi : SeedDataset.PriceOther, date, source));

    public Task<long> InsertDeliveryAsync(SeedFarmerPlot f, long orderId, long farmerId, long plotId, string date, double? sugarGl, string source) =>
        origins.InsertDeliveryAsync(new NewDelivery(orderId, plotId, farmerId, date, f.Kg, sugarGl, null, 1, source));

    public async Task<long> InsertOperationAsync(long batchId, string winery, SeedOperation o, string source, string method = "paper log")
    {
        long? from = o.From == null ? null : await vessels.GetIdAsync(winery, o.From);
        long? to = o.To == null ? null : await vessels.GetIdAsync(winery, o.To);
        return await operations.InsertAsync(new NewOperation(batchId, o.Date, o.Type, from, to, o.Litres, o.Bottles, o.Ml, o.Remainder, method, null, null, o.Note, source,
            DateTime.UtcNow.ToString("o")));
    }

    public async Task InsertA3Async(long batchId, int day)
    {
        var f1 = await vessels.GetIdAsync("W01", "F-1");
        await lab.InsertAsync(new NewLabRow(batchId, SeedDataset.A3Date(day), f1, "fermentation", SeedDataset.A3Sugar[day], SeedDataset.A3Temp[day], null, null, null,
            $"day {day + 1}", "Dataset A3"));
        await InsertDatasetReadingAsync(batchId, f1, SeedDataset.A3Date(day), "temperature", SeedDataset.A3Temp[day], "Dataset A3", $"day {day + 1}");
    }

    public async Task InsertA41Async(long batchId, SeedPhTaRow r)
    {
        await lab.InsertAsync(new NewLabRow(batchId, r.Date, null, "fermentation", null, null, r.Ph, r.Ta, null, $"fermentation day {r.Day}", "Dataset A4.1"));
        await InsertDatasetReadingAsync(batchId, null, r.Date, "ph", r.Ph, "Dataset A4.1", $"fermentation day {r.Day}");
        await InsertDatasetReadingAsync(batchId, null, r.Date, "ta", r.Ta, "Dataset A4.1", $"fermentation day {r.Day}");
    }

    public async Task InsertA42Async(long batchId, SeedLabRow r)
    {
        var v = await vessels.GetIdAsync("W03", r.Vessel);
        await lab.InsertAsync(new NewLabRow(batchId, r.Date, v, r.Stage, null, r.Temp, r.Ph, r.Ta, r.Rs, null, "Dataset A4.2"));
        await InsertDatasetReadingAsync(batchId, v, r.Date, "temperature", r.Temp, "Dataset A4.2", r.Stage);
        await InsertDatasetReadingAsync(batchId, v, r.Date, "ph", r.Ph, "Dataset A4.2", r.Stage);
        await InsertDatasetReadingAsync(batchId, v, r.Date, "ta", r.Ta, "Dataset A4.2", r.Stage);
    }

    public Task InsertDatasetReadingAsync(long batchId, long? vesselId, string date, string type, double value, string source, string? note) =>
        readings.InsertAsync(new NewReading(batchId, vesselId, date + "T09:00", type, value, ReadingTypes.Units[type], null, null,
            note == null ? SeedDataset.NoTime : $"{SeedDataset.NoTime} · {note}", source));
}
