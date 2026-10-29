namespace BacchusLedger.Services;

public sealed class ReportService : IReportService
{
    public Task<ServiceResult<object>> GetAsync(string? batch, string? vessel)
    {
        using var c = Db.Open();
        return Task.FromResult(LedgerReport.Report(c, string.IsNullOrWhiteSpace(batch) ? null : batch, string.IsNullOrWhiteSpace(vessel) ? null : vessel) is { } r
            ? ServiceResult<object>.Ok(r) : ServiceResult<object>.NotFound("batch not found"));
    }
}
