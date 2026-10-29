namespace BacchusLedger.Domain.Losses;

/// Normal loss per stage in % (DEMO ASSUMPTIONS, editable in Settings as norm_&lt;stage&gt;).
public sealed class StageNorms(IReadOnlyDictionary<string, double> percentByStage)
{
    public static readonly string[] Keys = ["racking_first", "racking", "filtration", "bottling", "stock_check", "transfer"];

    public bool TryGet(string stageKey, out double percent) => percentByStage.TryGetValue(stageKey, out percent);
}
