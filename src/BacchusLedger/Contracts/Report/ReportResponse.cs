namespace BacchusLedger.Contracts.Report;

/// The printable report of one batch, from vineyard to bottle as a double-entry ledger.
public sealed record ReportResponse(
    ReportBatchDto Batch,
    string? Vessel,
    IReadOnlyList<TimelineRowDto> Timeline,
    IReadOnlyList<LedgerEntryDto> Entries,
    IReadOnlyList<LedgerAccountDto> Accounts,
    LedgerBalanceDto Balance,
    IReadOnlyList<ReportLabRowDto> Lab,
    IReadOnlyList<ReportReadingDto> Readings,
    SimulatedSectionDto? Simulated,
    string GeneratedAt);
