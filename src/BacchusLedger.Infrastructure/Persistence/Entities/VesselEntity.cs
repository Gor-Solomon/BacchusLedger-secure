namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table vessels: A tank or qvevri.
public sealed class VesselEntity
{
    public long Id { get; set; }
    public long? WineryId { get; set; }
    public string? Code { get; set; }
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? CalibrationJson { get; set; }
    public double? CapacityL { get; set; }
    public string? Source { get; set; }
    public string? CellarLabel { get; set; }
    public string? CellarLabelSource { get; set; }
    public double? DepthCm { get; set; }
    public double? NeckDCm { get; set; }
    public double? MaxDCm { get; set; }
    public double? KnownVolumeL { get; set; }
}
