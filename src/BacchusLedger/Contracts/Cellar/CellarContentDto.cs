namespace BacchusLedger.Contracts.Cellar;

public sealed record CellarContentDto(string? Batch, long BatchId, double Litres, string? Source);
