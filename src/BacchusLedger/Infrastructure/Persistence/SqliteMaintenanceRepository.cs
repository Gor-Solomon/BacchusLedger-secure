using BacchusLedger.Repositories;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class SqliteMaintenanceRepository(ISqliteConnectionFactory connections) : IMaintenanceRepository
{
    /// A row in "maintenance" (inside the reset transaction only) is the one exception to the append-only triggers.
    public async Task AllowDeleteAsync(Func<Task> work)
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync("INSERT INTO maintenance(reason) VALUES('demo reset')");
        await work();
        await db.ExecAsync("DELETE FROM maintenance");
    }
}
