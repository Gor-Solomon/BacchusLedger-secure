namespace BacchusLedger.Domain.Readings;

/// A reading as entered (attached to a batch and optionally a vessel).
public sealed record ReadingInput(long BatchId, long? VesselId, string TakenAt, string Type, double Value, string? Instrument, string? Person, string? Note, string Source);
