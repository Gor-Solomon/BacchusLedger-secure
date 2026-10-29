namespace BacchusLedger.Models;

public sealed record NewOrder(string WineryCode, long FarmerId, long PlotId, string Variety, double KgPlanned, double PriceGelPerKg, string Date, string Source);
