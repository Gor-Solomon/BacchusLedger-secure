namespace BacchusLedger.Models;

public sealed record NewReading(
    long BatchId, long? VesselId, string TakenAt, string Type, double Value, string Unit, string? Instrument, string? Person, string? Note, string Source);
