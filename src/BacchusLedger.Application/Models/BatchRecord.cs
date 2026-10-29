namespace BacchusLedger.Application.Models;

/// A batch with its winery.
public sealed record BatchRecord(
    long Id,
    string Code,
    string? Name,
    string? WineType,
    string? Technology,
    string? Variety,
    long? Vintage,
    double? Abv,
    string? Status,
    string? SourceNote,
    double? GrapesKg,
    string WineryCode,
    string? WineryName)
{
    /// Demo replays, practice copies and the live-demo batch are copies, left out of the cellar totals.
    public bool IsCopy => (SourceNote ?? "").Contains("copy");
}
