namespace BacchusLedger.Application.Models;

public sealed record NewBatch(
    string WineryCode, string Code, string? Name, string? WineType, string? Technology, string? Variety,
    long? Vintage, double? Abv, string? Status, string? SourceNote, double? GrapesKg);
