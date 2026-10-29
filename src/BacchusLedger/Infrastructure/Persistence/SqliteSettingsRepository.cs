using BacchusLedger.Dataset;
using BacchusLedger.Repositories;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class SqliteSettingsRepository(ISqliteConnectionFactory connections) : ISettingsRepository
{
    public async Task<Dictionary<string, string>> GetAsync()
    {
        var d = SettingDefaults.Copy();
        await using var db = await connections.OpenAsync();
        foreach (var (k, v) in await db.QueryAsync("SELECT key, value FROM settings", r => (r.Str("key")!, r.Str("value")!))) d[k] = v;
        return d;
    }

    public async Task SaveAsync(IEnumerable<KeyValuePair<string, string?>> values)
    {
        await using var db = await connections.OpenAsync();
        foreach (var (k, v) in values) await db.ExecAsync("INSERT OR REPLACE INTO settings(key,value) VALUES($1,$2)", k, v);
    }
}
