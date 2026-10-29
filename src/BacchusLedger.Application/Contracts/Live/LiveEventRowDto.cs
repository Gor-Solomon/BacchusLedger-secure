namespace BacchusLedger.Application.Contracts.Live;

public sealed record LiveEventRowDto(
    long Id,
    string? CreatedAt,
    string? SimTime,
    long? VesselId,
    string? VesselCode,
    string? VesselName,
    string? Kind,
    string? Metric,
    double? Value,
    string? Unit,
    string? Level,
    string? Rule,
    string? Message,
    double? ValueUsd,
    string? Scenario,
    string? Source,
    long? Acknowledged);
