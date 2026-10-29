namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table batch_origins: Which delivery (kg) went into which batch.
public sealed class BatchOriginEntity
{
    /// SQLite's implicit rowid (the table declares no key).
    public long Rowid { get; set; }
    public long? BatchId { get; set; }
    public long? DeliveryId { get; set; }
    public double? Kg { get; set; }
}
