namespace BacchusLedger.Application.Contracts.Report;

public sealed record ReportBatchDto(
    string? Code, string? Name, string? Winery, string? WineryName, string? WineType, string? Technology, string? Variety,
    long? Vintage, double? Abv, string? Status, double? GrapesKg, string? Source);
