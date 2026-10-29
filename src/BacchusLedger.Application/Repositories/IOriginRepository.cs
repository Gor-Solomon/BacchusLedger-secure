using BacchusLedger.Application.Contracts.Batches;
using BacchusLedger.Application.Contracts.Origins;
using BacchusLedger.Application.Models;
using BacchusLedger.Domain.Ledger;

namespace BacchusLedger.Application.Repositories;

/// Farmers, plots, grape orders, deliveries and the deliveries of each batch.
public interface IOriginRepository
{
    Task<List<FarmerDto>> ListFarmersAsync();
    Task<long> CountFarmersAsync();
    Task<long?> FindFarmerIdAsync(string code);
    Task<long> InsertFarmerAsync(string code, string name, string? phone, bool isDemo);

    /// InPdoZone is left false (the caller knows the PDO list).
    Task<List<PlotDto>> ListPlotsAsync();
    Task<long?> FindPlotIdAsync(string code);
    Task<long> InsertPlotAsync(NewPlot plot);

    /// Throws when the plot does not exist.
    Task<long?> GetPlotFarmerIdAsync(long plotId);

    Task<long> InsertOrderAsync(NewOrder order);
    Task DeleteOrderAsync(long id);

    Task<List<DeliveryDto>> ListDeliveriesAsync();
    Task<long> InsertDeliveryAsync(NewDelivery delivery);
    Task<double?> GetDeliveryKgAsync(long id);
    Task DeleteDeliveryAsync(long id);

    Task InsertBatchOriginAsync(long batchId, long deliveryId, double? kg);
    Task DeleteBatchOriginsAsync(long batchId);
    Task<List<BatchOriginDto>> ListBatchOriginsAsync(long batchId);
    Task<List<OriginCheckRow>> ListOriginChecksAsync(long batchId);
    Task<List<HarvestDelivery>> ListLedgerOriginsAsync(long batchId);
    Task<List<FarmerOrigin>> ListFarmerOriginsAsync(long batchId);
    Task<List<OriginLink>> ListOriginLinksAsync(string batchCode);
}
