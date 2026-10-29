using System.Text.Json;
using BacchusLedger.Contracts.Common;
using BacchusLedger.Contracts.Origins;

namespace BacchusLedger.Services;

/// Farmers, plots and grape deliveries (where a batch comes from).
public interface IOriginService
{
    Task<IReadOnlyList<FarmerDto>> ListFarmersAsync();
    Task<ServiceResult<IdResponse>> AddFarmerAsync(JsonElement body);
    Task<IReadOnlyList<PlotDto>> ListPlotsAsync();
    Task<ServiceResult<IdResponse>> AddPlotAsync(JsonElement body);
    Task<IReadOnlyList<DeliveryDto>> ListDeliveriesAsync();
    Task<ServiceResult<IdResponse>> AddDeliveryAsync(JsonElement body);
}
