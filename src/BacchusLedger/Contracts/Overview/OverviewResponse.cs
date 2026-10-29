namespace BacchusLedger.Contracts.Overview;

/// Flags holds StageFlag and CheckFlag items (each serialized with its own fields).
public sealed record OverviewResponse(
    OverviewKpi Kpi,
    IReadOnlyList<object> Flags,
    IReadOnlyList<OnePercentRow> OnePercent,
    IReadOnlyDictionary<string, string> Settings);
