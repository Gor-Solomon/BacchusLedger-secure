namespace BacchusLedger.Application.Contracts.Cellar;

public sealed record CellarResponse(
    string Date,
    IReadOnlyList<CellarVesselDto> Vessels,
    IReadOnlyCollection<string> Dates,
    IReadOnlyDictionary<string, double> Totals,
    string PeakDate,
    double PeakLitres);
