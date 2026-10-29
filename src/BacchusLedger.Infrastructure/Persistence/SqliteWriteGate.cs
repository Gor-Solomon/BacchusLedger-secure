namespace BacchusLedger.Infrastructure.Persistence;

/// Serialises write transactions (SQLite allows one writer anyway; this avoids "database is locked" between two writers).
public sealed class SqliteWriteGate
{
    readonly SemaphoreSlim gate = new(1, 1);

    public Task WaitAsync() => gate.WaitAsync();

    public void Release() => gate.Release();
}
