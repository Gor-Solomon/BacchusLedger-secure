namespace BacchusLedger.Application.Contracts.Seal;

/// DEMO ONLY: the sealed value that was changed.
public sealed record TamperResponse(long Entry, string Table, long RowId, string? Date, string Field, double Before, double After);
