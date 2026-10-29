using System.Text.Json.Serialization;

namespace BacchusLedger.Contracts.Overview;

/// "What 1% of loss costs per year" for one winery.
public sealed record OnePercentRow(
    string Winery,
    string? Name,
    string? Size,
    double YearlyT,
    [property: JsonPropertyName("litres_1pct")] double Litres1Pct,
    double Usd,
    bool UpTo);
