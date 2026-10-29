namespace BacchusLedger.Models;

/// A reading with its batch, vessel and winery.
public sealed record ReadingRecord(
    long Id, long? BatchId, long? VesselId, string? TakenAt, string Type, double Value, string? Unit, string? Instrument, string? Person, string? Note,
    string? Source, string? BatchCode, string? VesselCode, string? CellarLabel, string? Winery);
