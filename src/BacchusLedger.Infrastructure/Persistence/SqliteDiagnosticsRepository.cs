using BacchusLedger.Application.Repositories;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class SqliteDiagnosticsRepository(ISqliteConnectionFactory connections) : IDiagnosticsRepository
{
    static readonly string[] Tables = ["operations", "readings", "lab", "deliveries", "orders", "batches", "batch_origins", "vessels", "live_events"];

    public async Task<long> CountRowsAsync(string table)
    {
        if (!Tables.Contains(table)) throw new ArgumentException($"unknown table {table}");
        await using var db = await connections.OpenAsync();
        return await db.CountAsync($"SELECT COUNT(*) n FROM {table}");
    }

    public async Task<string> SnapshotAsync()
    {
        await using var db = await connections.OpenAsync();
        var tables = await db.QueryAsync("SELECT name FROM sqlite_master WHERE type='table' AND name NOT IN ('live_events') AND name NOT LIKE 'sqlite_%' ORDER BY name", r => r.Str("name")!);
        var parts = new List<string>();
        foreach (var t in tables)
            parts.Add($"{t}:" + (await db.QueryAsync($"SELECT COUNT(*) n, COALESCE(MAX(rowid),0) m FROM \"{t}\"", r => $"{r.Value("n")}/{r.Value("m")}/"))[0]);
        var settings = await db.QueryAsync("SELECT key, value FROM settings ORDER BY key", r => $"{r.Str("key")}={r.Str("value")}");
        return string.Join(";", parts) + ";settings=" + string.Join(",", settings);
    }

    public async Task<string> TryChangeAppendOnlyAsync(string table, bool delete)
    {
        var sql = (table, delete) switch
        {
            ("readings", false) => "UPDATE readings SET value=value+1 WHERE id=(SELECT MIN(id) FROM readings)",
            ("readings", true) => "DELETE FROM readings WHERE id=(SELECT MIN(id) FROM readings)",
            ("operations", false) => "UPDATE operations SET litres=0 WHERE id=(SELECT MIN(id) FROM operations)",
            ("operations", true) => "DELETE FROM operations WHERE id=(SELECT MIN(id) FROM operations)",
            _ => throw new ArgumentException($"not an append-only table: {table}"),
        };
        await using var db = await connections.OpenAsync();
        try { await db.ExecAsync(sql); return "allowed"; }
        catch (RepositoryException e) { return e.Message.Contains("append-only") ? "blocked" : e.Message; }
    }
}
