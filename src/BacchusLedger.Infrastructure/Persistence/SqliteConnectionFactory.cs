using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BacchusLedger.Infrastructure.Persistence;

/// Temporary bridge while the repositories move to EF Core: hand-written SQL runs on the same connection and transaction as the DbContext.
public sealed class SqliteConnectionFactory(BacchusLedgerDbContext db) : ISqliteConnectionFactory
{
    public async Task<SqliteSession> OpenAsync()
    {
        await db.Database.OpenConnectionAsync();
        return new SqliteSession((SqliteConnection)db.Database.GetDbConnection(), db.Database.CurrentTransaction?.GetDbTransaction() as SqliteTransaction, owned: false);
    }
}
