using BacchusLedger.Application.Contracts.Overview;

namespace BacchusLedger.Application.Contracts.Demo;

public sealed record OnePercentExtra(IReadOnlyList<OnePercentRow> OnePercent);
