namespace BacchusLedger.Application.Contracts.Live;

/// Allowed range of one measurement and whether the last reading was inside it (null = not checked yet).
public sealed record RangeStateDto(double Min, double Max, string Unit, string From, bool? InRange);
