using BacchusLedger.Application.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory(IOptions<StorageOptions> options) : ISqliteConnectionFactory
{
    readonly SemaphoreSlim writeLock = new(1, 1);
    readonly AsyncLocal<SqliteSession?> current = new();

    public async Task<SqliteSession> OpenAsync() => current.Value ?? new(await OpenConnectionAsync(), null, owned: true);

    public async Task RunExclusiveAsync(Func<SqliteSession, Task> work)
    {
        await writeLock.WaitAsync();
        try
        {
            await using var session = new SqliteSession(await OpenConnectionAsync(), null, owned: true);
            await work(session);
        }
        finally { writeLock.Release(); }
    }

    public async Task<T> RunInTransactionAsync<T>(Func<Task<T>> work, bool commit)
    {
        if (current.Value != null && commit) return await work();
        await writeLock.WaitAsync();
        try
        {
            await using var connection = await OpenConnectionAsync();
            await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync();
            current.Value = new SqliteSession(connection, tx, owned: false);
            try
            {
                var result = await work();
                if (commit) await tx.CommitAsync(); else await tx.RollbackAsync();
                return result;
            }
            finally { current.Value = null; }
        }
        finally { writeLock.Release(); }
    }

    async Task<SqliteConnection> OpenConnectionAsync()
    {
        var c = new SqliteConnection($"Data Source={options.Value.DbPath}");
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
        await cmd.ExecuteNonQueryAsync();
        return c;
    }
}
