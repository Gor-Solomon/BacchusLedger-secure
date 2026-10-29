namespace BacchusLedger.Application.Models;

/// Batch code and wine type (which default fermentation range applies).
public sealed record BatchWineType(string? Code, string? WineType);
