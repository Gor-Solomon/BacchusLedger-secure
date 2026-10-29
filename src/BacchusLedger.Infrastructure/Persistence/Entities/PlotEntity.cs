namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table plots: A vineyard plot with its terroir data.
public sealed class PlotEntity
{
    public long Id { get; set; }
    public long? FarmerId { get; set; }
    public long? WineryId { get; set; }
    public string? Code { get; set; }
    public string? CadastreNo { get; set; }
    public string? Village { get; set; }
    public string? Municipality { get; set; }
    public string? Region { get; set; }
    public double? AreaHa { get; set; }
    public string? Variety { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string? LicenceNo { get; set; }
    public string? SoilTexture { get; set; }
    public double? SoilPh { get; set; }
    public double? RainMm { get; set; }
    public double? SummerTempC { get; set; }
    public long? IsDemo { get; set; }
}
