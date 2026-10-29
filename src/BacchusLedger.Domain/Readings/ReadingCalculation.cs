namespace BacchusLedger.Domain.Readings;

/// Values calculated for one reading; all null when nothing applies.
public sealed record ReadingCalculation(double? PotentialAbv, double? Sg, double? ProgressPct, string? Formula);
