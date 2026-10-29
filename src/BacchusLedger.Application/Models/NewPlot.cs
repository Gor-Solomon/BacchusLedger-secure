namespace BacchusLedger.Application.Models;

public sealed record NewPlot(
    long FarmerId, string? WineryCode, string? Code, string? CadastreNo, string? Village, string? Municipality, string? Region, double? AreaHa,
    string? Variety, double? Lat, double? Lng, string? LicenceNo, string? SoilTexture, double? SoilPh, double? RainMm, double? SummerTempC, long IsDemo);
