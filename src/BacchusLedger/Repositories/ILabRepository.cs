using BacchusLedger.Contracts.Batches;
using BacchusLedger.Contracts.Readings;
using BacchusLedger.Contracts.Report;
using BacchusLedger.Models;

namespace BacchusLedger.Repositories;

public interface ILabRepository
{
    Task<List<LabRowDto>> ListForBatchAsync(long batchId);
    Task<List<ReportLabRowDto>> ListForReportAsync(long batchId);

    /// The first lab sugar value (g/L) of each batch, filtered by batch and/or vessel.
    Task<List<InitialSugarDto>> InitialSugarAsync(long? batchId, long? vesselId);

    Task InsertAsync(NewLabRow row);
    Task<List<DatasetLabRow>> ListDatasetRowsAsync();

    /// Demo reset only (inside IMaintenanceRepository.AllowDeleteAsync).
    Task DeleteForBatchAsync(long batchId);
}
