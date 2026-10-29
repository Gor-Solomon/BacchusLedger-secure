using BacchusLedger.Application.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

/// One DbContext transaction per write; repositories save into it. A nested call joins the running transaction.
public sealed class EfUnitOfWork(BacchusLedgerDbContext db, SqliteWriteGate gate) : IUnitOfWork
{
    public Task<T> WriteAsync<T>(Func<Task<T>> work) => RunAsync(work, commit: true);

    public Task<T> ProbeAsync<T>(Func<Task<T>> work) => RunAsync(work, commit: false);

    async Task<T> RunAsync<T>(Func<Task<T>> work, bool commit)
    {
        if (db.Database.CurrentTransaction != null && commit) return await work();
        await gate.WaitAsync();
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                var result = await work();
                if (commit)
                {
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                else await tx.RollbackAsync();
                return result;
            }
            finally { db.ChangeTracker.Clear(); }
        }
        finally { gate.Release(); }
    }
}
