using System.Text.Json;
using BacchusLedger.Application.Contracts.Common;
using BacchusLedger.Application.Contracts.Origins;
using BacchusLedger.Domain.Dataset;
using BacchusLedger.Application.Extensions;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;

namespace BacchusLedger.Application.Services;

public sealed class OriginService(IOriginRepository origins, IUnitOfWork unitOfWork) : IOriginService
{
    public async Task<IReadOnlyList<FarmerDto>> ListFarmersAsync() => await origins.ListFarmersAsync();

    public async Task<ServiceResult<IdResponse>> AddFarmerAsync(JsonElement body)
    {
        var name = body.Str("name");
        if (string.IsNullOrEmpty(name)) return ServiceResult<IdResponse>.BadRequest("Name is required");
        var phone = body.RawStr("phone");
        var id = await unitOfWork.WriteAsync(async () =>
            await origins.InsertFarmerAsync("F-" + (await origins.CountFarmersAsync() + 1).ToString("000"), name, phone, isDemo: false));
        return new IdResponse(id);
    }

    public async Task<IReadOnlyList<PlotDto>> ListPlotsAsync() =>
        (await origins.ListPlotsAsync()).Select(p => p with { InPdoZone = SeedDataset.PdoMunicipalities.Contains(p.Municipality ?? "") }).ToList();

    public async Task<ServiceResult<IdResponse>> AddPlotAsync(JsonElement body)
    {
        if (body.Num("farmer_id") is not { } fid || string.IsNullOrEmpty(body.Str("code"))) return ServiceResult<IdResponse>.BadRequest("Farmer and plot code are required");
        try
        {
            var plot = new NewPlot((long)fid, body.Str("winery"), body.Str("code"), body.Str("cadastre_no"), body.Str("village"), body.Str("municipality"), "Kakheti",
                body.Num("area_ha"), body.Str("variety"), null, null, body.Str("licence_no"), null, null, null, null, 0);
            return new IdResponse(await unitOfWork.WriteAsync(() => origins.InsertPlotAsync(plot)));
        }
        catch (RepositoryException e) { return ServiceResult<IdResponse>.BadRequest(e.Message); }
    }

    public async Task<IReadOnlyList<DeliveryDto>> ListDeliveriesAsync() => await origins.ListDeliveriesAsync();

    public async Task<ServiceResult<IdResponse>> AddDeliveryAsync(JsonElement body)
    {
        if (body.Num("plot_id") is not { } pid || body.Num("kg") is not { } kg || kg <= 0) return ServiceResult<IdResponse>.BadRequest("Plot and kg are required");
        var id = await unitOfWork.WriteAsync(async () =>
        {
            var fid = await origins.GetPlotFarmerIdAsync((long)pid);
            var did = await origins.InsertDeliveryAsync(new NewDelivery(null, (long)pid, fid, body.RawStr("date") ?? DateTime.UtcNow.ToString("yyyy-MM-dd"), kg,
                body.Num("sugar_gl"), body.RawStr("quality_note"), (body.Num("accepted") ?? 1) > 0 ? 1 : 0, "Entered"));
            if (body.Num("batch_id") is { } bid) await origins.InsertBatchOriginAsync((long)bid, did, kg);
            return did;
        });
        return new IdResponse(id);
    }
}
