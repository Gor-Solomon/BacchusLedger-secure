namespace BacchusLedger.Repositories;

public interface IMaintenanceRepository
{
    /// Runs work with the append-only delete guard lifted (demo reset only), inside the running transaction.
    Task AllowDeleteAsync(Func<Task> work);
}
