namespace BacchusLedger.Application.Contracts.Batches;

public sealed record CheckDto(string Key, string Label, string Status, string Detail, string Source);
