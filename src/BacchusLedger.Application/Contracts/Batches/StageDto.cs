namespace BacchusLedger.Application.Contracts.Batches;

/// One replayed operation with its litres, loss and normal loss.
public sealed record StageDto(
    long OpId,
    string Date,
    string Type,
    string StageKey,
    string? From,
    string? To,
    string? FromLabel,
    string? ToLabel,
    double? BeforeL,
    double? AfterL,
    double? RemainderL,
    double? LossL,
    double? LossPct,
    double? NormPct,
    double? NormL,
    double? ExcessL,
    string Status,
    int? Bottles,
    double? BottleMl,
    string? Method,
    double? DipCm,
    string? Note,
    string? Source,
    string? Person,
    StageComparisonDto? Other);
