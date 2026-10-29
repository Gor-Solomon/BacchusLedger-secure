using BacchusLedger.Contracts.Batches;

namespace BacchusLedger.Services;

public interface IBatchService
{
    Task<IReadOnlyList<BatchSummaryDto>> ListAsync();

    /// Stages, checks, origins, lab rows and totals of one batch.
    Task<ServiceResult<BatchDetailResponse>> GetAsync(long id);
}
