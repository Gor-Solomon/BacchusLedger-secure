namespace BacchusLedger.Repositories;

/// Rows of the journals that come straight from the records (1, 2, 3, 4, 7 and 8; 5 and 6 come from the loss engine).
/// Each row maps column key to value and has a "source".
public interface IJournalRepository
{
    Task<List<Dictionary<string, object?>>> RowsAsync(int journal, long? batch, string? winery);
}
