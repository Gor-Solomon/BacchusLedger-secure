namespace BacchusLedger.Domain.Ledger;

/// One grape delivery of a batch, as booked in the ledger.
public sealed record HarvestDelivery(long DeliveryId, string Date, double? Kg, string? Source, string? Plot, string? Municipality, string? Farmer);
