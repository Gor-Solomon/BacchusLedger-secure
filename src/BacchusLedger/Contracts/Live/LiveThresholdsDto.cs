namespace BacchusLedger.Contracts.Live;

/// TempMax = F-2's allowed maximum from config/ranges.json (the chart's dashed line).
public sealed record LiveThresholdsDto(double TempMax, double LevelDropL, double StuckMinSg);
