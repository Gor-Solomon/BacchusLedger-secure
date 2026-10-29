namespace BacchusLedger.Application.Models;

/// Qvevri measurements and the calibration made from them.
public sealed record VesselMeasurements(
    double? DepthCm, double? NeckDCm, double? MaxDCm, double? KnownVolumeL, string? CalibrationJson, double? CapacityL, string? Source);
