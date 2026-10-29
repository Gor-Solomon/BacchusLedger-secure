namespace BacchusLedger.Domain.Losses;

/// One replayed operation: litres before/after, loss, normal loss and status (ok | above | negative | n/a).
public sealed class LossStage
{
    public long OpId { get; set; }
    public string Date { get; set; } = "";
    public string Type { get; set; } = "";
    public string StageKey { get; set; } = "";
    public string? From { get; set; }
    public string? To { get; set; }
    public string? FromLabel { get; set; }
    public string? ToLabel { get; set; }
    public double? BeforeL { get; set; }
    public double? AfterL { get; set; }
    public double? RemainderL { get; set; }
    public double? LossL { get; set; }
    public double? LossPct { get; set; }
    public double? NormPct { get; set; }
    public double? NormL { get; set; }
    public double? ExcessL { get; set; }
    public string Status { get; set; } = "n/a";
    public int? Bottles { get; set; }
    public double? BottleMl { get; set; }
    public string? Method { get; set; }
    public double? DipCm { get; set; }
    public string? Note { get; set; }
    public string? Source { get; set; }
    public string? Person { get; set; }
    public StageComparison? Other { get; set; }
}
