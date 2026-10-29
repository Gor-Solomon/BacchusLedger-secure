namespace BacchusLedger.Models;

/// Who delivered the grapes of a batch.
public sealed record FarmerOrigin(string? Farmer, string? Plot, string? Municipality, double? Kg, string? Source);
