namespace BacchusLedger.Application.Contracts.Live;

public sealed record LiveVesselDto(
    long Id,
    string Code,
    string Winery,
    string Name,
    string Type,
    bool Fermenting,
    double Temp,
    double Level,
    double? Capacity,
    double? Density,
    string Status,
    IReadOnlyDictionary<string, string> Alerts,
    string? Batch,
    string WineColor,
    IReadOnlyDictionary<string, RangeStateDto> Ranges);
