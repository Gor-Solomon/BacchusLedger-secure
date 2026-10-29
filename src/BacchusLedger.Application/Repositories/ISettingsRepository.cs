namespace BacchusLedger.Application.Repositories;

public interface ISettingsRepository
{
    /// The defaults overridden by the stored values.
    Task<Dictionary<string, string>> GetAsync();

    Task SaveAsync(IEnumerable<KeyValuePair<string, string?>> values);
}
