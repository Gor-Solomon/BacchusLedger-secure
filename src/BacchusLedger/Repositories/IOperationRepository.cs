using BacchusLedger.Domain.Losses;

namespace BacchusLedger.Repositories;

public interface IOperationRepository
{
    /// The batch's operations in date order, with vessel codes and cellar labels.
    Task<List<CellarOperation>> ListForBatchAsync(long batchId);

    Task<long> InsertAsync(Models.NewOperation operation);
    Task<string?> FirstDateAsync(long batchId);

    /// True when an operation on the vessel was recorded at or after sinceIso (created_at).
    Task<bool> AnyRecordedSinceAsync(long vesselId, string sinceIso);

    /// Demo reset only (inside IMaintenanceRepository.AllowDeleteAsync).
    Task DeleteForBatchAsync(long batchId);
}
