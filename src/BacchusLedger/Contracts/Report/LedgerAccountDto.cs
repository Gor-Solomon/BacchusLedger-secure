namespace BacchusLedger.Contracts.Report;

public sealed record LedgerAccountDto(string Name, string Unit, double Debit, double Credit, double Balance);
