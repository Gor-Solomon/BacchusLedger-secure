using BacchusLedger.Contracts.Overview;

namespace BacchusLedger.Contracts.Demo;

public sealed record OnePercentExtra(IReadOnlyList<OnePercentRow> OnePercent);
