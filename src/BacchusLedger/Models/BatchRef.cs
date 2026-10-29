namespace BacchusLedger.Models;

/// Batch id, code and winery (for per-vessel contents).
public sealed record BatchRef(long Id, string Code, string? SourceNote, string WineryCode)
{
    public bool IsCopy => (SourceNote ?? "").Contains("copy");
}
