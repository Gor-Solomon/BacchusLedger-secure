namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table orders: A grape order from a farmer.
public sealed class OrderEntity
{
    public long Id { get; set; }
    public long? WineryId { get; set; }
    public long? FarmerId { get; set; }
    public long? PlotId { get; set; }
    public string? Variety { get; set; }
    public double? KgPlanned { get; set; }
    public double? PriceGelPerKg { get; set; }
    public string? Date { get; set; }
    public long? IsDemo { get; set; }
    public string? Source { get; set; }
}
