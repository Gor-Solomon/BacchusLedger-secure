namespace BacchusLedger.Domain.Losses;

/// The same stage at the other winery (W01 vs W03).
public sealed record StageComparison(string Winery, string Batch, double? LossL, double? LossPct, string Status, string? Source);
