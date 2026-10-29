namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table operations: A cellar operation (append-only, sealed in the hash chain).
public sealed class OperationEntity
{
    public long Id { get; set; }
    public long? BatchId { get; set; }
    public string? Date { get; set; }
    public string? Type { get; set; }
    public long? FromVesselId { get; set; }
    public long? ToVesselId { get; set; }
    public double? Litres { get; set; }
    public long? Bottles { get; set; }
    public double? BottleMl { get; set; }
    public double? RemainderL { get; set; }
    public string? Method { get; set; }
    public double? DipCm { get; set; }
    public string? Person { get; set; }
    public string? Note { get; set; }
    public string? Source { get; set; }
    public string? CreatedAt { get; set; }
}
