namespace BacchusLedger.Contracts.Batches;

/// One delivery that went into a batch, with its plot and terroir data.
public sealed record BatchOriginDto(
    double? Kg,
    double? SharePct,
    long DeliveryId,
    string? Date,
    double? SugarGl,
    string? Source,
    string? Farmer,
    string? Plot,
    double? AreaHa,
    string? Variety,
    string? Municipality,
    string? Region,
    string? CadastreNo,
    double? Lat,
    double? Lng,
    double? SoilPh,
    string? SoilTexture,
    double? RainMm,
    double? SummerTempC);
