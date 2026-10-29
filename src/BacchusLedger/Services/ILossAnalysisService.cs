namespace BacchusLedger.Services;

/// Mass balance of a batch: replays its operations and adds the origin, PDO and qvevri checks.
public interface ILossAnalysisService
{
    /// settings: null = the stored settings. extra: an operation not saved yet (preview).
    /// compare: add the same stage at the other demo winery (W01 vs W03). norms: override the normal losses.
    Task<LossEngine.Result> AnalyzeAsync(long batchId, IReadOnlyDictionary<string, string>? settings = null, LossEngine.Op? extra = null,
        bool compare = true, Dictionary<string, double>? norms = null);
}
