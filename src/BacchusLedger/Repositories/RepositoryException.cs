namespace BacchusLedger.Repositories;

/// The database refused a change (constraint, append-only trigger). The message is the database's own text.
public sealed class RepositoryException(string message, Exception inner) : Exception(message, inner);
