using BacchusLedger.Application.Contracts.Overview;

namespace BacchusLedger.Application.Services;

public interface IOverviewService
{
    /// Cellar KPIs, flags of every batch and the "1% of loss" table.
    Task<OverviewResponse> GetAsync();

    Task<IReadOnlyList<WineryDto>> GetWineriesAsync();
}
