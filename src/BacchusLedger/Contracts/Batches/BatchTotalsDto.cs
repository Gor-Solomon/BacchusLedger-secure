namespace BacchusLedger.Contracts.Batches;

public sealed record BatchTotalsDto(
    double? FirstLitres,
    double BottledL,
    int Bottles,
    double RemainderL,
    double TotalLossL,
    double? TotalDifferenceL,
    double LitresNow,
    double LossValueUsd,
    double ExcessL,
    IReadOnlyDictionary<string, double> Vessels);
