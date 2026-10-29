namespace BacchusLedger.Infrastructure.Persistence;

public interface ISqliteConnectionFactory
{
    /// The DbContext's connection, inside its running transaction if there is one.
    Task<SqliteSession> OpenAsync();
}
