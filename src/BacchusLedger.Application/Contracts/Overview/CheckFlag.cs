namespace BacchusLedger.Application.Contracts.Overview;

/// A failed (or informational) batch check.
public sealed record CheckFlag(string Batch, long BatchId, bool Copy, string Level, string Text, string Detail, string Source);
