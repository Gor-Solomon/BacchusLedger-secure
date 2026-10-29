using BacchusLedger.Application.Contracts.Vessels;
using BacchusLedger.Application.Models;

namespace BacchusLedger.Application.Repositories;

public interface IVesselRepository
{
    /// By internal code or by cellar label (what is written on the tape in the cellar); the exact code wins.
    Task<VesselRecord?> FindAsync(string codeOrLabel, string? winery);

    Task<VesselRecord?> GetAsync(string winery, string code);

    /// Throws when the vessel does not exist.
    Task<long> GetIdAsync(string winery, string code);

    Task<bool> ExistsAsync(string winery, string code);

    /// Ordered by winery and id.
    Task<List<VesselRecord>> ListAsync();

    /// Ordered by winery, qvevri first, then id (cellar map).
    Task<List<VesselRecord>> ListForCellarAsync();

    /// Vessels of the batch's winery with this code.
    Task<List<long>> ListIdsAsync(long batchId, string code);

    /// Batches that used the vessel, most recent first.
    Task<List<VesselBatchDto>> ListBatchesUsingAsync(long vesselId);

    /// The real batch that used the vessel last.
    Task<BatchWineType?> LastRealBatchAsync(long vesselId);

    Task<string?> FirstRealWineTypeAsync(string winery);
    Task<bool> LabelUsedAsync(long wineryId, long exceptVesselId, string label);
    Task<long> InsertAsync(NewVessel vessel);
    Task SetLabelAsync(long id, string? label, string? labelSource);
    Task SetMeasurementsAsync(long id, VesselMeasurements measurements);

    /// Live-demo vessels no record refers to.
    Task DeleteUnusedLiveDemoAsync();
    Task ClearLiveDemoLabelsAsync();
}
