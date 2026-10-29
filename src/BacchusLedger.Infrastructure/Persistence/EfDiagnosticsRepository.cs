using BacchusLedger.Application.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class EfDiagnosticsRepository(BacchusLedgerDbContext db) : IDiagnosticsRepository
{
    public Task<long> CountRowsAsync(string table) => table switch
    {
        "operations" => db.Operations.LongCountAsync(),
        "readings" => db.Readings.LongCountAsync(),
        "lab" => db.Lab.LongCountAsync(),
        "deliveries" => db.Deliveries.LongCountAsync(),
        "orders" => db.Orders.LongCountAsync(),
        "batches" => db.Batches.LongCountAsync(),
        "batch_origins" => db.BatchOrigins.LongCountAsync(),
        "vessels" => db.Vessels.LongCountAsync(),
        "live_events" => db.LiveEvents.LongCountAsync(),
        _ => throw new ArgumentException($"unknown table {table}"),
    };

    /// Row count and last rowid of every table except live_events and the outbox tables (written by Live mode), plus all settings.
    public async Task<string> SnapshotAsync()
    {
        // Raw SQL on purpose: sqlite_master and rowid have no EF Core model (they cover every table, also ones the model does not know).
        var tables = await db.Database
            .SqlQuery<string>($"SELECT name AS Value FROM sqlite_master WHERE type='table' AND name NOT IN ('live_events','outbox_messages','processed_messages') AND name NOT LIKE 'sqlite_%' ORDER BY name")
            .ToListAsync();
        var parts = new List<string>();
        foreach (var t in tables)
        {
            // Whitelist: only names of existing tables (from sqlite_master above) made of letters and underscores go into the SQL text.
            if (!t.All(c => char.IsAsciiLetter(c) || c == '_')) continue;
            var sql = "SELECT COUNT(*) AS Rows, COALESCE(MAX(rowid),0) AS LastRowid FROM \"" + t + "\"";
            var c = await db.Database.SqlQueryRaw<TableStats>(sql).SingleAsync();
            parts.Add($"{t}:{c.Rows}/{c.LastRowid}/");
        }
        var settings = await db.Settings.AsNoTracking().OrderBy(s => s.Key).Select(s => s.Key + "=" + s.Value).ToListAsync();
        return string.Join(";", parts) + ";settings=" + string.Join(",", settings);
    }

    /// Tries to change or delete the oldest row of an append-only table, to prove the triggers work (run it inside a rolled-back probe).
    public async Task<string> TryChangeAppendOnlyAsync(string table, bool delete)
    {
        // Raw SQL on purpose: it must hit the real database triggers. Whitelist: only these four fixed statements.
        var sql = (table, delete) switch
        {
            ("readings", false) => "UPDATE readings SET value=value+1 WHERE id=(SELECT MIN(id) FROM readings)",
            ("readings", true) => "DELETE FROM readings WHERE id=(SELECT MIN(id) FROM readings)",
            ("operations", false) => "UPDATE operations SET litres=0 WHERE id=(SELECT MIN(id) FROM operations)",
            ("operations", true) => "DELETE FROM operations WHERE id=(SELECT MIN(id) FROM operations)",
            _ => throw new ArgumentException($"not an append-only table: {table}"),
        };
        try { await db.Database.ExecuteSqlRawAsync(sql); return "allowed"; }
        catch (SqliteException e) { return e.Message.Contains("append-only") ? "blocked" : e.Message; }
    }

    sealed record TableStats(long Rows, long LastRowid);
}
