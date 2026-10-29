namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table demo_rows: A row written by a demo player (removed again by the demo reset).
public sealed class DemoRowEntity
{
    /// SQLite's implicit rowid (the table declares no key).
    public long Rowid { get; set; }
    public string? Script { get; set; }
    public string? Tbl { get; set; }
    public long? RowId { get; set; }
}
