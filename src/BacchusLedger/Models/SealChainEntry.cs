namespace BacchusLedger.Models;

/// One stored chain entry.
public sealed record SealChainEntry(long Seq, string Tbl, long RowId, string Prev, string Hash);
