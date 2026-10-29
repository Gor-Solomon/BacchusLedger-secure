namespace BacchusLedger.Contracts.Readings;

/// Initial sugar from the lab in g/L (shown as is, never converted to Brix).
public sealed record InitialSugarDto(string? Date, double? SugarGl, string? Source, string? BatchCode);
