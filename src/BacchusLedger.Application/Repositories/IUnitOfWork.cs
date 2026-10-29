namespace BacchusLedger.Application.Repositories;

/// Groups repository calls into one database transaction.
public interface IUnitOfWork
{
    /// Runs work in one transaction and commits it. Writes are serialised; a nested call joins the running transaction.
    Task<T> WriteAsync<T>(Func<Task<T>> work);

    /// Runs work in a transaction that is always rolled back (selftest probes): the database is not changed.
    Task<T> ProbeAsync<T>(Func<Task<T>> work);
}
