namespace BacchusLedger.Contracts.Cellar;

/// Grapes waiting in a vessel between intake and separation.
public sealed record CellarGrapesDto(string? Batch, long BatchId, double? Kg, string Since, string? Source);
