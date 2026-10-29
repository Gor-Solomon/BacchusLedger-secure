namespace BacchusLedger.Infrastructure.Persistence;

public interface ISqliteConnectionFactory
{
    /// The session of the running unit of work, or a new open connection.
    Task<SqliteSession> OpenAsync();

    /// Runs work in one transaction on one connection; writes are serialised (SQLite allows one writer anyway).
    /// commit = false rolls everything back (selftest probes). A nested call joins the running transaction.
    Task<T> RunInTransactionAsync<T>(Func<Task<T>> work, bool commit);

    /// Runs work on its own connection without a transaction, holding the write lock (schema reset).
    Task RunExclusiveAsync(Func<SqliteSession, Task> work);
}
