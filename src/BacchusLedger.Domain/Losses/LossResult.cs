namespace BacchusLedger.Domain.Losses;

/// Mass balance of one batch.
public sealed class LossResult
{
    public long BatchId { get; set; }
    public string BatchCode { get; set; } = "";
    public string Winery { get; set; } = "";
    public List<LossStage> Stages { get; set; } = [];
    public List<LossCheck> Checks { get; set; } = [];
    public double? FirstLitres { get; set; }
    public double BottledL { get; set; }
    public int Bottles { get; set; }
    public double RemainderL { get; set; }
    public double TotalLossL { get; set; }
    public double? TotalDifferenceL { get; set; }
    public double LitresNow { get; set; }
    public double LossValueUsd { get; set; }
    public double ExcessL { get; set; }
    public Dictionary<string, double> VesselLitres { get; set; } = [];
}
