namespace BacchusLedger.Application.Models;

public sealed record NewDelivery(
    long? OrderId, long PlotId, long? FarmerId, string Date, double Kg, double? SugarGl, string? QualityNote, long Accepted, string Source);
