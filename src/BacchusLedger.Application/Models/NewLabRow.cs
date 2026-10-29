namespace BacchusLedger.Application.Models;

public sealed record NewLabRow(
    long BatchId, string Date, long? VesselId, string? Stage, double? SugarGl, double? TempC, double? Ph, double? TaGl, double? RsGl, string? Note, string Source);
