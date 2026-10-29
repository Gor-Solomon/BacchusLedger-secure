namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table maintenance: A row here (inside the demo reset transaction only) lifts the append-only delete guard.
public sealed class MaintenanceEntity
{
    /// SQLite's implicit rowid (the table declares no key).
    public long Rowid { get; set; }
    public string? Reason { get; set; }
}
