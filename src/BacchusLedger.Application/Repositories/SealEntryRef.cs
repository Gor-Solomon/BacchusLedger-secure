namespace BacchusLedger.Application.Repositories;

/// A record row that belongs in a batch's hash chain.
public sealed record SealEntryRef(string Table, long RowId);
