using BacchusLedger.Domain.Losses;

namespace BacchusLedger.Domain.Ledger;

public sealed record Ledger(List<LedgerEntry> Entries, List<LedgerAccount> Accounts, LedgerBalance Balance, LossResult Engine);
