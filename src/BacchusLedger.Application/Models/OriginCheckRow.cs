namespace BacchusLedger.Application.Models;

/// Kg and plot of one delivery in a batch (origin and PDO checks).
public sealed record OriginCheckRow(double? Kg, string? Plot, string? Municipality);
