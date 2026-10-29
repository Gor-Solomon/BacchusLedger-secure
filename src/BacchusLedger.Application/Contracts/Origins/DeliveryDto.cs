namespace BacchusLedger.Application.Contracts.Origins;

public sealed record DeliveryDto(
    long Id,
    long? OrderId,
    long? PlotId,
    long? FarmerId,
    string? Date,
    double? Kg,
    double? SugarGl,
    string? QualityNote,
    long? Accepted,
    string? Source,
    string? Farmer,
    string? Plot,
    string? Winery,
    string? Batches);
