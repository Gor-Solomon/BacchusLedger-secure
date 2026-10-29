namespace BacchusLedger.Contracts.Seal;

/// One sealed record entry: hash = SHA-256(prev | table | row).
public sealed record SealEntryDto(long Seq, string Tbl, long RowId, string Hash, string Prev);
