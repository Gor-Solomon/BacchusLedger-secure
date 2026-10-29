namespace BacchusLedger.Models;

/// The LIVE demo scenarios (SIMULATED).
public static class LiveScenarios
{
    public static readonly string[] All = ["hot", "loss", "stuck", "missing", "allgood"];

    public static readonly IReadOnlyDictionary<string, string> Titles = new Dictionary<string, string>
    {
        ["hot"] = "Hot fermentation", ["loss"] = "Unrecorded wine loss", ["stuck"] = "Stuck fermentation", ["missing"] = "Missing data", ["allgood"] = "All good",
    };
}
