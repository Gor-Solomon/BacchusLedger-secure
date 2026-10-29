namespace BacchusLedger.Contracts.Report;

/// One side (debit or credit) of a ledger entry.
public sealed record LedgerSideDto(string Account, double Amount, string Unit);
