namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table deliveries: A grape delivery (sealed in the hash chain).
public sealed class DeliveryEntity
{
    public long Id { get; set; }
    public long? OrderId { get; set; }
    public long? PlotId { get; set; }
    public long? FarmerId { get; set; }
    public string? Date { get; set; }
    public double? Kg { get; set; }
    public double? SugarGl { get; set; }
    public string? QualityNote { get; set; }
    public long? Accepted { get; set; }
    public string? Source { get; set; }
}
