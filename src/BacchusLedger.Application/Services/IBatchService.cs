using BacchusLedger.Application.Contracts.Batches;

namespace BacchusLedger.Application.Services;

public interface IBatchService
{
    Task<IReadOnlyList<BatchSummaryDto>> ListAsync();

    /// Stages, checks, origins, lab rows and totals of one batch.
    Task<ServiceResult<BatchDetailResponse>> GetAsync(long id);
}
