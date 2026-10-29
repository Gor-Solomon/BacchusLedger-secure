namespace BacchusLedger.Models;

/// One delivery of a batch as booked in the ledger.
public sealed record LedgerOrigin(long DeliveryId, string Date, double? Kg, string? Source, string? Plot, string? Municipality, string? Farmer);
