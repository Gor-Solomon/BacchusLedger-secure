using BacchusLedger.Contracts.Journals;

namespace BacchusLedger.Services;

/// The 8 Kakheti PDO journals, generated from the records.
public interface IJournalService
{
    /// Which of the 8 journals have rows (for the coverage tiles).
    Task<IReadOnlyList<JournalCoverageDto>> CoverageAsync(long? batch, string? winery);

    Task<ServiceResult<JournalDto>> GetAsync(int n, long? batch, string? winery);

    Task<ServiceResult<JournalCsvFile>> CsvAsync(int n, long? batch, string? winery);
}
