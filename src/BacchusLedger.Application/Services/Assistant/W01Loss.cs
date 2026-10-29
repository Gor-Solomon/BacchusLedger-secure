namespace BacchusLedger.Application.Services.Assistant;

public sealed record W01Loss(long BatchId, string Code, List<LossStep> Steps, double TotalLossL, double RemainderL, double ValueUsd, double ValuePerLitre);
