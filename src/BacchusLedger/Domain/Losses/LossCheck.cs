namespace BacchusLedger.Domain.Losses;

/// A batch check. Status: pass | fail | info | na.
public sealed class LossCheck
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Status { get; set; } = "pass";
    public string Detail { get; set; } = "";
    public string Source { get; set; } = "Calculated";
}
