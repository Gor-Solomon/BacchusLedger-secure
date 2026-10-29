namespace BacchusLedger.Models;

public sealed record NewVessel(
    string WineryCode, string Code, string Type, string Name, string? CalibrationJson, double? CapacityL, string Source,
    string? CellarLabel = null, string? CellarLabelSource = null, double? DepthCm = null, double? NeckDCm = null, double? MaxDCm = null, double? KnownVolumeL = null);
