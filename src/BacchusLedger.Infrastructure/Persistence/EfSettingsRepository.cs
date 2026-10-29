using BacchusLedger.Application.Repositories;
using BacchusLedger.Domain.Dataset;
using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class EfSettingsRepository(BacchusLedgerDbContext db) : ISettingsRepository
{
    public async Task<Dictionary<string, string>> GetAsync()
    {
        var d = SettingDefaults.Copy();
        foreach (var s in await db.Settings.AsNoTracking().ToListAsync()) d[s.Key] = s.Value!;
        return d;
    }

    /// Insert or replace each value (as INSERT OR REPLACE did).
    public async Task SaveAsync(IEnumerable<KeyValuePair<string, string?>> values)
    {
        foreach (var (k, v) in values)
            if (await db.Settings.Where(s => s.Key == k).ExecuteUpdateAsync(u => u.SetProperty(s => s.Value, v)).GuardAsync() == 0)
                await db.InsertAsync(new SettingEntity { Key = k, Value = v });
    }
}
