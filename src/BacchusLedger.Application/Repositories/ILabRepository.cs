using BacchusLedger.Application.Contracts.Batches;
using BacchusLedger.Application.Contracts.Readings;
using BacchusLedger.Application.Contracts.Report;
using BacchusLedger.Application.Models;

namespace BacchusLedger.Application.Repositories;

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
