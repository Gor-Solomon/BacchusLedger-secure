namespace BacchusLedger.Application.Contracts.Batches;

public sealed record LabRowDto(
    long Id,
    long? BatchId,
    string? Date,
    long? VesselId,
    string? Stage,
    double? SugarGl,
    double? TempC,
    double? Ph,
    double? TaGl,
    double? RsGl,
    string? Note,
    string? Source,
    string? Vessel);
