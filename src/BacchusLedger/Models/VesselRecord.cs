namespace BacchusLedger.Models;

/// A tank or qvevri with its winery code.
public sealed record VesselRecord(
    long Id,
    long WineryId,
    string WineryCode,
    string Code,
    string Type,
    string? Name,
    string? CalibrationJson,
    double? CapacityL,
    string? Source,
    string? CellarLabel,
    string? CellarLabelSource,
    double? DepthCm,
    double? NeckDCm,
    double? MaxDCm,
    double? KnownVolumeL);
