namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table batches: A wine batch.
public sealed class BatchEntity
{
    public long Id { get; set; }
    public long? WineryId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? WineType { get; set; }
    public string? Technology { get; set; }
    public string? Variety { get; set; }
    public long? Vintage { get; set; }
    public double? Abv { get; set; }
    public string? Status { get; set; }
    public string? SourceNote { get; set; }
    public double? GrapesKg { get; set; }
}
