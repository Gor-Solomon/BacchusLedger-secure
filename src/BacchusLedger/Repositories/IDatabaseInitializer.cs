namespace BacchusLedger.Repositories;

public interface IDatabaseInitializer
{
    /// Creates or migrates the schema and seeds an empty database; reset drops all demo data first.
    Task InitializeAsync(bool reset);

    /// Reset demo data: the only place where all rows are removed.
    Task ResetAndSeedAsync();
}
