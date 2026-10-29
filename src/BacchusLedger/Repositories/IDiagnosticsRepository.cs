namespace BacchusLedger.Repositories;

/// Read-only checks for the selftest.
public interface IDiagnosticsRepository
{
    Task<long> CountRowsAsync(string table);

    /// Row counts and last row ids of every table except live_events, plus all settings.
    Task<string> SnapshotAsync();

    /// Tries to change or delete the oldest row of an append-only table ("readings" or "operations"):
    /// "blocked", "allowed" or the database error. Run it inside a rolled-back probe.
    Task<string> TryChangeAppendOnlyAsync(string table, bool delete);
}
