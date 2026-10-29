namespace BacchusLedger.Contracts.Readings;

public sealed record ReadingRowDto(
    long Id,
    long? BatchId,
    long? VesselId,
    string? TakenAt,
    string? Type,
    double? Value,
    string? Unit,
    string? Instrument,
    string? Person,
    string? Note,
    string? Source,
    string? BatchCode,
    string? VesselCode,
    string? CellarLabel,
    string? Winery,
    ReadingCalcDto Calc,
    bool TimeInDataset);
