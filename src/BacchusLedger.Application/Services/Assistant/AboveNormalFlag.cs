namespace BacchusLedger.Application.Services.Assistant;

public sealed record AboveNormalFlag(long BatchId, string Batch, string Stage, string Date, double? LossL, double? LossPct, double? NormPct, double? ExcessL, string? Source);
