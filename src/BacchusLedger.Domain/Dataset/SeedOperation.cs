namespace BacchusLedger.Domain.Dataset;

public sealed record SeedOperation(string Date, string Type, string? From, string? To, double? Litres, int? Bottles = null, double? Ml = null, double? Remainder = null, string? Note = null);
