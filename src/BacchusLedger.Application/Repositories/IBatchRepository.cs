using BacchusLedger.Application.Models;

namespace BacchusLedger.Application.Repositories;

public interface IBatchRepository
{
    Task<BatchRecord?> GetAsync(long id);
    Task<BatchRecord?> GetByCodeAsync(string code);
    Task<long?> FindIdAsync(string code);

    /// All batches, ordered by code.
    Task<List<BatchRecord>> ListAsync();

    /// Real batches only (no demo or practice copies).
    Task<List<BatchRecord>> ListRealAsync();
    Task<List<long>> ListRealIdsAsync();

    Task<List<BatchRef>> ListRefsAsync();

    /// All ids, oldest first.
    Task<List<long>> ListIdsAsync();

    /// Ids of the batches matching an optional batch id and winery code, ordered by code.
    Task<List<long>> ListIdsAsync(long? batch, string? winery);

    Task<long> CountCodesLikeAsync(string pattern);
    Task<long> InsertAsync(NewBatch batch);
    Task SetStatusAsync(string code, string status);
    Task DeleteAsync(long id);
}
