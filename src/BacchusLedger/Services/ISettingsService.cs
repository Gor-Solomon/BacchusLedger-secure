using System.Text.Json;
using BacchusLedger.Contracts.Settings;

namespace BacchusLedger.Services;

/// Editable settings (value per litre, normal losses, live thresholds) and the demo data reset.
public interface ISettingsService
{
    Task<IReadOnlyDictionary<string, string>> GetAsync();

    /// Saves known settings (numbers, except language); the running simulator uses them from the next tick.
    Task<ServiceResult<IReadOnlyDictionary<string, string>>> SaveAsync(IReadOnlyDictionary<string, JsonElement> values);

    /// Drops and re-seeds all demo data.
    Task<ResetResponse> ResetAsync();
}
