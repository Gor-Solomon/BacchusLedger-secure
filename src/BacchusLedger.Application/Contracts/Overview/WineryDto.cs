namespace BacchusLedger.Application.Contracts.Overview;

public sealed record WineryDto(long Id, string? Code, string? Name, string? Size, double? YearlyT, long? UsesQvevri, string? ReadinessLevel);
