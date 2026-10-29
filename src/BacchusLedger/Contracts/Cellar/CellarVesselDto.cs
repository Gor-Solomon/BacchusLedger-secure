namespace BacchusLedger.Contracts.Cellar;

/// Outline: [z / H, radius / max radius] pairs for drawing.
public sealed record CellarVesselDto(
    long Id,
    string? Winery,
    string? Code,
    string? CellarLabel,
    string? Name,
    string? Type,
    double? CapacityL,
    string? CapacitySource,
    bool Calibrated,
    double? DCm,
    double? HCm,
    IReadOnlyList<double[]>? Outline,
    double Litres,
    IReadOnlyList<CellarContentDto> Contents,
    CellarGrapesDto? Grapes,
    string Status,
    string? StatusReason);
