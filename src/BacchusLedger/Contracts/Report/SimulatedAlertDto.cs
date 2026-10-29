namespace BacchusLedger.Contracts.Report;

public sealed record SimulatedAlertDto(
    string? CreatedAt, string? SimTime, string? Level, string? Rule, string? Message, string? Scenario, string? Source, long? Acknowledged);
