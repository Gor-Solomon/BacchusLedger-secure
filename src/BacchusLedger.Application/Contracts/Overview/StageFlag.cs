namespace BacchusLedger.Application.Contracts.Overview;

/// A stage above normal (or with more wine than recorded).
public sealed record StageFlag(
    string Batch,
    long BatchId,
    bool Copy,
    string Level,
    string Date,
    string Stage,
    double? LossL,
    double? LossPct,
    double? NormPct,
    double? ExcessL,
    string Text,
    string Source);
