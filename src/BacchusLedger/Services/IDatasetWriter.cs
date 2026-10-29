using BacchusLedger.Dataset;

namespace BacchusLedger.Services;

/// Writes dataset rows (seed data and the demo replays) through the repositories.
public interface IDatasetWriter
{
    Task<long> InsertBatchAsync(SeedBatch batch, string code, string status, string sourceNote);

    /// Farmer and plot, created once (by code).
    Task<(long FarmerId, long PlotId)> EnsureFarmerPlotAsync(SeedFarmerPlot f);

    Task<long> InsertOrderAsync(SeedFarmerPlot f, long farmerId, long plotId, string date, string source);
    Task<long> InsertDeliveryAsync(SeedFarmerPlot f, long orderId, long farmerId, long plotId, string date, double? sugarGl, string source);
    Task<long> InsertOperationAsync(long batchId, string winery, SeedOperation o, string source, string method = "paper log");

    /// Dataset A3 day d: lab row and temperature reading in F-1.
    Task InsertA3Async(long batchId, int day);

    /// Dataset A4.1 row: lab row and pH/TA readings.
    Task InsertA41Async(long batchId, SeedPhTaRow row);

    /// Dataset A4.2 row: lab row and temperature/pH/TA readings.
    Task InsertA42Async(long batchId, SeedLabRow row);

    /// A dataset reading: dates only, stored at 09:00 and marked "time not in dataset".
    Task InsertDatasetReadingAsync(long batchId, long? vesselId, string date, string type, double value, string source, string? note);
}
