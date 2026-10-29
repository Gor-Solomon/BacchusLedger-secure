using BacchusLedger.Repositories;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class SqliteUnitOfWork(ISqliteConnectionFactory connections) : IUnitOfWork
{
    public Task<T> WriteAsync<T>(Func<Task<T>> work) => connections.RunInTransactionAsync(work, commit: true);

    public Task<T> ProbeAsync<T>(Func<Task<T>> work) => connections.RunInTransactionAsync(work, commit: false);
}
