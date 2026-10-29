namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table seal_tamper: DEMO ONLY: a value changed by the tamper action, to put it back.
public sealed class SealTamperEntity
{
    /// SQLite's implicit rowid (the table declares no key).
    public long Rowid { get; set; }
    public string? Tbl { get; set; }
    public long? RowId { get; set; }
    public string? Col { get; set; }
    public double? OldValue { get; set; }
    public long? BatchId { get; set; }
}
