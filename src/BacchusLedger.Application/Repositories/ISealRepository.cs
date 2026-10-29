using BacchusLedger.Application.Models;

namespace BacchusLedger.Application.Repositories;

/// Hash chain and seal per batch, plus the demo tamper log.
public interface ISealRepository
{
    Task EnsureTablesAsync();
    Task<string?> GetVersionAsync();
    Task SetVersionAsync(string version);

    /// Removes every chain, seal and tamper record (chain format change).
    Task ClearAllAsync();

    Task<List<SealChainEntry>> ChainAsync(long batchId);
    Task<SealRecord?> GetAsync(long batchId);

    /// The batch's record entries in chain order: deliveries, operations, lab, readings.
    Task<List<SealEntryRef>> CurrentEntriesAsync(long batchId);

    /// All columns of a record row (null when the row is gone).
    Task<Dictionary<string, object?>?> GetRowAsync(string table, long id);

    Task AddEntryAsync(long batchId, long seq, string table, long rowId, string prev, string hash);
    Task SaveAsync(SealRecord seal);
    Task DeleteChainAndSealAsync(long batchId);
    Task DeleteTamperRecordsAsync(long batchId);
    Task<List<TamperRecord>> TamperRecordsAsync(long batchId);
    Task<SealedLabTemperature?> FirstSealedLabTemperatureAsync(long batchId);
    Task SetLabTemperatureAsync(long labId, double? value);
    Task<bool> TamperRecordedAsync(long labId);
    Task AddTamperRecordAsync(long labId, double oldValue, long batchId);
}
