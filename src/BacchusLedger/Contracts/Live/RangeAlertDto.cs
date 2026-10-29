namespace BacchusLedger.Contracts.Live;

public sealed record RangeAlertDto(
    string VesselCode, string VesselName, string? Batch, string Metric, string Direction, double Value, double Min, double Max, string Unit, string Since, string Source);
