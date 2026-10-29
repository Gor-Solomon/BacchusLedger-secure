namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table readings: A fermentation reading (append-only, sealed in the hash chain).
public sealed class ReadingEntity
{
    public long Id { get; set; }
    public long? BatchId { get; set; }
    public long? VesselId { get; set; }
    public string? TakenAt { get; set; }
    public string? Type { get; set; }
    public double? Value { get; set; }
    public string? Unit { get; set; }
    public string? Instrument { get; set; }
    public string? Person { get; set; }
    public string? Note { get; set; }
    public string? Source { get; set; }
}
