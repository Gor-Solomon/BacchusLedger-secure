using BacchusLedger.Application.Contracts.Batches;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Application.Services.Mapping;

namespace BacchusLedger.Application.Services;

public sealed class BatchService(
    IBatchRepository batches, IOperationRepository operations, IOriginRepository origins, ILabRepository lab, ISettingsRepository settings,
    ILossAnalysisService losses) : IBatchService
{
    public async Task<IReadOnlyList<BatchSummaryDto>> ListAsync()
    {
        var s = await settings.GetAsync();
        var list = new List<BatchSummaryDto>();
        foreach (var b in await batches.ListAsync()) list.Add(await SummaryAsync(b, s));
        return list;
    }

    public async Task<ServiceResult<BatchDetailResponse>> GetAsync(long id)
    {
        if (await batches.GetAsync(id) is not { } b) return ServiceResult<BatchDetailResponse>.NotFound("batch not found");
        var s = await settings.GetAsync();
        var r = await losses.AnalyzeAsync(id, s);
        var totals = new BatchTotalsDto(r.FirstLitres, r.BottledL, r.Bottles, r.RemainderL, r.TotalLossL, r.TotalDifferenceL, r.LitresNow, r.LossValueUsd, r.ExcessL, r.VesselLitres);
        return new BatchDetailResponse(await SummaryAsync(b, s), r.Stages.ToDtos(), r.Checks.ToDtos(), await origins.ListBatchOriginsAsync(id),
            await lab.ListForBatchAsync(id), totals, s);
    }

    async Task<BatchSummaryDto> SummaryAsync(BatchRecord b, IReadOnlyDictionary<string, string> s)
    {
        var r = await losses.AnalyzeAsync(b.Id, s, compare: false);
        var intake = r.Stages.FirstOrDefault(x => x.Type == "intake")?.Date ?? await operations.FirstDateAsync(b.Id);
        return new BatchSummaryDto(b.Id, b.Code, b.WineryCode, b.Name, b.WineType, b.Technology, b.Variety, b.Vintage, b.Abv, b.Status, b.GrapesKg, b.SourceNote, b.IsCopy,
            intake, r.FirstLitres, r.LitresNow, r.BottledL, r.Bottles, r.TotalLossL, r.LossValueUsd,
            r.Stages.Count(x => x.Status == "above"), r.Stages.Count(x => x.Status == "negative"));
    }
}
