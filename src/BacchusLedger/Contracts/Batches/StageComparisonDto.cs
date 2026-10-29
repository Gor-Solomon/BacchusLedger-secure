namespace BacchusLedger.Contracts.Batches;

public sealed record StageComparisonDto(string Winery, string Batch, double? LossL, double? LossPct, string Status, string? Source);
