using BacchusLedger.Application.Repositories;
using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class EfMaintenanceRepository(BacchusLedgerDbContext db) : IMaintenanceRepository
{
    /// A row in "maintenance" (inside the reset transaction only) is the one exception to the append-only delete triggers.
    public async Task AllowDeleteAsync(Func<Task> work)
    {
        await db.InsertAsync(new MaintenanceEntity { Reason = "demo reset" });
        await work();
        await db.Maintenance.ExecuteDeleteAsync().GuardAsync();
    }
}
