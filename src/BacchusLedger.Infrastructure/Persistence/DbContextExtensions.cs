using BacchusLedger.Application.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

public static class DbContextExtensions
{
    /// Adds the row and saves it at once (inside the running transaction, if any), so its new key is known.
    /// The entity is detached afterwards: reads never rely on tracked rows. Database errors become RepositoryException with the same message as before.
    public static async Task<T> InsertAsync<T>(this BacchusLedgerDbContext db, T entity) where T : class
    {
        db.Add(entity);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException e) when (e.InnerException is SqliteException s) { throw new RepositoryException(s.Message, s); }
        finally { db.Entry(entity).State = EntityState.Detached; }
        return entity;
    }

    /// A bulk update or delete (ExecuteUpdate / ExecuteDelete); database errors (e.g. the append-only triggers) become RepositoryException.
    public static async Task<int> GuardAsync(this Task<int> bulk)
    {
        try { return await bulk; }
        catch (SqliteException s) { throw new RepositoryException(s.Message, s); }
    }

    /// SQLite's lower() (ASCII letters only) and optionally trim() (spaces only), done in C# for a query parameter
    /// so labels compare exactly as before.
    public static string SqliteLower(this string s, bool trim) =>
        new((trim ? s.Trim(' ') : s).Select(c => c is >= 'A' and <= 'Z' ? (char)(c + 32) : c).ToArray());
}
