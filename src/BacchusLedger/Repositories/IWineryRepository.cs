using BacchusLedger.Contracts.Overview;

namespace BacchusLedger.Repositories;

public interface IWineryRepository
{
    /// Ordered by code.
    Task<List<WineryDto>> ListAsync();

    /// Ordered by yearly tonnes (the "1% of loss" table).
    Task<List<WineryDto>> ListBySizeAsync();

    Task InsertAsync(string code, string name, string size, double yearlyT, bool usesQvevri, string readinessLevel);
}
