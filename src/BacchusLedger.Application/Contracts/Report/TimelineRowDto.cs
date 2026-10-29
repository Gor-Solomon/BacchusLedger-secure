namespace BacchusLedger.Application.Contracts.Report;

public sealed record TimelineRowDto(string Date, string Step, string? Vessels, string Volume);
