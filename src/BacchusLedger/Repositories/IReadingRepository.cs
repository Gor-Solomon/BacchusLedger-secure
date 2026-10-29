using BacchusLedger.Contracts.Report;
using BacchusLedger.Models;

namespace BacchusLedger.Repositories;

/// Fermentation readings (append-only).
public interface IReadingRepository
{
    Task<List<ReadingRecord>> ListAsync(long? batchId, long? vesselId);
    Task<List<ReportReadingDto>> ListForReportAsync(long batchId);

    /// Temperature, pH and TA readings of a batch in time order.
    Task<List<LatestReading>> ListBasicAsync(long batchId);

    Task<long> InsertAsync(NewReading reading);

    /// Time of the first fermentation reading of the vessel/batch up to takenAt (any non-Brix reading, or a lab row at 09:00).
    Task<string?> FirstFermentationAsync(long batchId, long? vesselId, string takenAt);

    Task<bool> HasInitialBrixAsync(long batchId, long? vesselId);
    Task<double?> LatestBrixAsync(long batchId, long? vesselId);

    /// Last value of a reading type on a vessel.
    Task<double?> LastValueAsync(long vesselId, string type);

    Task<bool> VesselHasInitialBrixAsync(string vesselCode);
    Task<long> CountAsync(string batchCode, string type, string source);

    /// Brix/hydrometer readings that are on a seeded batch or not entered by a person.
    Task<long> CountInventedAsync(IReadOnlyList<string> seededBatchCodes);

    Task<long> CountFromDatasetAsync();

    /// Demo reset only (inside IMaintenanceRepository.AllowDeleteAsync).
    Task DeleteForBatchAsync(long batchId);
}
