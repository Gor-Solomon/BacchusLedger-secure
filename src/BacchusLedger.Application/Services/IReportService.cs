using BacchusLedger.Domain.Ledger;

namespace BacchusLedger.Application.Services;

/// Ledger report (read-only): a batch from vineyard to bottle as a double-entry ledger.
public interface IReportService
{
    /// A ReportResponse, or a VesselReportResponse when only a vessel ("W01/F-2") is given.
    Task<ServiceResult<object>> GetAsync(string? batch, string? vessel);

    /// ReportResponse, VesselReportResponse (no batch given) or null (batch not found).
    Task<object?> BuildAsync(string? batchCode, string? vessel);

    /// The double-entry ledger of one batch (null when the batch does not exist).
    Task<Ledger?> LedgerAsync(long batchId);
}
