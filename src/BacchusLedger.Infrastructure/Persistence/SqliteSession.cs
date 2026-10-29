using BacchusLedger.Application.Repositories;
using Microsoft.Data.Sqlite;

namespace BacchusLedger.Infrastructure.Persistence;

/// An open connection (and transaction, inside a unit of work). Parameters are positional: $1, $2, …
/// Disposing closes the connection only when this session owns it (not the shared unit-of-work connection).
public sealed class SqliteSession(SqliteConnection connection, SqliteTransaction? transaction, bool owned) : IAsyncDisposable
{
    public SqliteConnection Connection { get; } = connection;
    public SqliteTransaction? Transaction { get; } = transaction;

    /// Database errors (constraints, append-only triggers) come back as RepositoryException with the database's message.
    public async Task<int> ExecAsync(string sql, params object?[] args)
    {
        await using var cmd = Command(sql, args);
        try { return await cmd.ExecuteNonQueryAsync(); }
        catch (SqliteException e) { throw new RepositoryException(e.Message, e); }
    }

    /// Runs an INSERT and returns the new row id.
    public async Task<long> InsertAsync(string sql, params object?[] args)
    {
        await using var cmd = Command(sql + "; SELECT last_insert_rowid();", args);
        try { return (long)(await cmd.ExecuteScalarAsync())!; }
        catch (SqliteException e) { throw new RepositoryException(e.Message, e); }
    }

    public async Task<List<T>> QueryAsync<T>(string sql, Func<SqliteRow, T> map, params object?[] args)
    {
        await using var cmd = Command(sql, args);
        await using var reader = await cmd.ExecuteReaderAsync();
        var row = new SqliteRow(reader);
        var list = new List<T>();
        while (await reader.ReadAsync()) list.Add(map(row));
        return list;
    }

    public async Task<T?> FirstOrDefaultAsync<T>(string sql, Func<SqliteRow, T> map, params object?[] args) =>
        (await QueryAsync(sql, map, args)).FirstOrDefault();

    /// Rows as column → value maps (column order kept), for data whose columns are not fixed (journals, sealed row content).
    public Task<List<Dictionary<string, object?>>> QueryRowsAsync(string sql, params object?[] args) => QueryAsync(sql, r => r.ToDictionary(), args);

    public async Task<long> CountAsync(string sql, params object?[] args)
    {
        await using var cmd = Command(sql, args);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    SqliteCommand Command(string sql, object?[] args)
    {
        var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = sql;
        for (int i = 0; i < args.Length; i++) cmd.Parameters.AddWithValue("$" + (i + 1), args[i] ?? DBNull.Value);
        return cmd;
    }

    public async ValueTask DisposeAsync()
    {
        if (owned) await Connection.DisposeAsync();
    }
}
