namespace BacchusLedger.Application.Contracts.Report;

public sealed record LedgerEntryDto(
    int No, long? OpId, string? ChainRef, string Date, string Kind, string Text, string? Source, string? Ratio, LedgerSideDto Debit, LedgerSideDto Credit);
