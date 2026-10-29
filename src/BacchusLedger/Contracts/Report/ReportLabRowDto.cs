namespace BacchusLedger.Contracts.Report;

public sealed record ReportLabRowDto(
    long Id, string? Date, string? Vessel, string? Stage, double? SugarGl, double? TempC, double? Ph, double? TaGl, double? RsGl, string? Note, string? Source);
