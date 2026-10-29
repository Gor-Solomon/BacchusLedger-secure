namespace BacchusLedger.Models;

/// A value changed by the demo tamper action (to put it back).
public sealed record TamperRecord(long RowId, double? OldValue);
