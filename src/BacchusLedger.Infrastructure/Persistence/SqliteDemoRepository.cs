using BacchusLedger.Application.Repositories;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class SqliteDemoRepository(ISqliteConnectionFactory connections) : IDemoRepository
{
    public async Task<HashSet<long>> DoneStepsAsync(string script)
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync("SELECT step FROM demo_progress WHERE script=$1", r => r.Long("step"), script)).ToHashSet();
    }

    public async Task<bool> IsDoneAsync(string script, int step)
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync("SELECT 1 x FROM demo_progress WHERE script=$1 AND step=$2", r => 1, script, step)).Count > 0;
    }

    public async Task MarkDoneAsync(string script, int step)
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync("INSERT INTO demo_progress(script,step) VALUES($1,$2)", script, step);
    }

    public async Task TrackAsync(string script, string table, long id)
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync("INSERT INTO demo_rows(script,tbl,row_id) VALUES($1,$2,$3)", script, table, id);
    }

    public async Task<List<long>> TrackedAsync(string script, string table)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("SELECT row_id FROM demo_rows WHERE script=$1 AND tbl=$2 ORDER BY rowid", r => r.Long("row_id"), script, table);
    }

    public async Task ForgetAsync(string script)
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync("DELETE FROM demo_rows WHERE script=$1", script);
        await db.ExecAsync("DELETE FROM demo_progress WHERE script=$1", script);
    }
}
