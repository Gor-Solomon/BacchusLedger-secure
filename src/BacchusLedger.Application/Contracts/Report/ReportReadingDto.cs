namespace BacchusLedger.Application.Contracts.Report;

public sealed record ReportReadingDto(
    long Id, string? TakenAt, string? Vessel, string? Type, double? Value, string? Unit, string? Instrument, string? Person, string? Note, string? Source);
