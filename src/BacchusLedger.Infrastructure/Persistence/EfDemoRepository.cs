using BacchusLedger.Application.Repositories;
using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class EfDemoRepository(BacchusLedgerDbContext db) : IDemoRepository
{
    public async Task<HashSet<long>> DoneStepsAsync(string script) =>
        (await db.DemoProgress.AsNoTracking().Where(p => p.Script == script).Select(p => p.Step).ToListAsync()).ToHashSet();

    public Task<bool> IsDoneAsync(string script, int step) => db.DemoProgress.AnyAsync(p => p.Script == script && p.Step == step);

    public Task MarkDoneAsync(string script, int step) => db.InsertAsync(new DemoProgressEntity { Script = script, Step = step });

    public Task TrackAsync(string script, string table, long id) => db.InsertAsync(new DemoRowEntity { Script = script, Tbl = table, RowId = id });

    /// In the order they were written (rowid).
    public Task<List<long>> TrackedAsync(string script, string table) =>
        db.DemoRows.AsNoTracking().Where(r => r.Script == script && r.Tbl == table).OrderBy(r => r.Rowid).Select(r => r.RowId ?? 0).ToListAsync();

    public async Task ForgetAsync(string script)
    {
        await db.DemoRows.Where(r => r.Script == script).ExecuteDeleteAsync().GuardAsync();
        await db.DemoProgress.Where(p => p.Script == script).ExecuteDeleteAsync().GuardAsync();
    }
}
