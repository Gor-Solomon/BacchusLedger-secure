namespace BacchusLedger.Contracts.Readings;

/// The latest Initial Brix of a vessel/batch (corrections are new rows).
public sealed record InitialBrixDto(
    long? BatchId,
    string? BatchCode,
    string? VesselCode,
    string? CellarLabel,
    string? TakenAt,
    double InitialBrix,
    double PotentialAbv,
    double StartSg,
    string? Source);
