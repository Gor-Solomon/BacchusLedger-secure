using System.Text.Json;
using BacchusLedger.Application.Contracts.Common;
using BacchusLedger.Application.Contracts.Origins;

namespace BacchusLedger.Application.Services;

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
