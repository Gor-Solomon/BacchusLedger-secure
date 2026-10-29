namespace BacchusLedger.Services;

/// Ledger report (read-only): a batch from vineyard to bottle as a double-entry ledger.
public interface IReportService
{
    /// A ReportResponse, or a VesselReportResponse when only a vessel ("W01/F-2") is given.
    Task<ServiceResult<object>> GetAsync(string? batch, string? vessel);
}
