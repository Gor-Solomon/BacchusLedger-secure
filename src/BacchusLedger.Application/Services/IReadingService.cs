using System.Text.Json;
using BacchusLedger.Application.Contracts.Readings;
using BacchusLedger.Domain.Readings;

namespace BacchusLedger.Application.Services;

/// Manual fermentation readings (timestamped, attached to a vessel and its batch). Append-only.
public interface IReadingService
{
    Task<ServiceResult<ReadingListResponse>> ListAsync(long? batch, string? vessel, string? winery);

    /// Validates the readings and returns their calculations and warnings without saving.
    Task<ServiceResult<ReadingPreviewResponse>> PreviewAsync(JsonElement body);

    Task<ServiceResult<ReadingsSavedResponse>> SaveAsync(JsonElement body);

    /// Saves one reading and returns its warnings.
    Task<(long id, List<string> warnings)> InsertAsync(ReadingInput reading);

    /// Warnings for a reading (e.g. Initial Brix after fermentation started).
    Task<List<string>> WarningsAsync(ReadingInput reading);
}
