namespace BacchusLedger.Domain.Ledger;

/// Totals of one account (name + unit).
public sealed record LedgerAccount(string Name, string Unit, double Debit, double Credit)
{
    public double Balance => Math.Round(Debit - Credit, 2);
}
