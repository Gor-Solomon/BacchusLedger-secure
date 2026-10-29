using System.Linq.Expressions;
using BacchusLedger.Application.Contracts.Vessels;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class EfVesselRepository(BacchusLedgerDbContext db) : IVesselRepository
{
    static readonly Expression<Func<VesselWithWinery, VesselRecord>> ToRecord = x => new VesselRecord(x.V.Id, x.V.WineryId ?? 0, x.W.Code!, x.V.Code!, x.V.Type!,
        x.V.Name, x.V.CalibrationJson, x.V.CapacityL, x.V.Source, x.V.CellarLabel, x.V.CellarLabelSource, x.V.DepthCm, x.V.NeckDCm, x.V.MaxDCm, x.V.KnownVolumeL);

    /// By internal code, or by cellar label (same lower(trim(…)) comparison as before); the exact code wins.
    public Task<VesselRecord?> FindAsync(string codeOrLabel, string? winery)
    {
        var label = codeOrLabel.SqliteLower(trim: true);
        return Joined()
            .Where(x => (x.V.Code == codeOrLabel || ((x.V.CellarLabel ?? "") != "" && x.V.CellarLabel!.Trim().ToLower() == label)) && (winery == null || x.W.Code == winery))
            .OrderByDescending(x => x.V.Code == codeOrLabel).ThenBy(x => x.V.Id).Select(ToRecord).FirstOrDefaultAsync();
    }

    public Task<VesselRecord?> GetAsync(string winery, string code) => Joined().Where(x => x.W.Code == winery && x.V.Code == code).Select(ToRecord).FirstOrDefaultAsync();

    public async Task<long> GetIdAsync(string winery, string code) =>
        (await GetAsync(winery, code))?.Id ?? throw new InvalidOperationException($"vessel {winery}/{code} not found");

    public async Task<bool> ExistsAsync(string winery, string code) => await GetAsync(winery, code) != null;

    public Task<List<VesselRecord>> ListAsync() => Joined().OrderBy(x => x.W.Code).ThenBy(x => x.V.Id).Select(ToRecord).ToListAsync();

    public Task<List<VesselRecord>> ListForCellarAsync() =>
        Joined().OrderBy(x => x.W.Code).ThenByDescending(x => x.V.Type).ThenBy(x => x.V.Id).Select(ToRecord).ToListAsync();

    public Task<List<long>> ListIdsAsync(long batchId, string code) =>
        (from v in db.Vessels.AsNoTracking()
         join b in db.Batches on v.WineryId equals b.WineryId
         where b.Id == batchId && v.Code == code
         select v.Id).ToListAsync();

    /// Most recent first; real batches before copies on the same date.
    public async Task<List<VesselBatchDto>> ListBatchesUsingAsync(long vesselId) =>
        (await (from u in Uses(vesselId)
                orderby u.Last descending, EF.Functions.Like(u.SourceNote, "%copy%"), u.Id
                select new { u.Id, u.Code, u.Last }).ToListAsync())
        .Select(x => new VesselBatchDto(x.Id, x.Code, x.Last)).ToList();

    public Task<BatchWineType?> LastRealBatchAsync(long vesselId) =>
        (from u in Uses(vesselId)
         where !EF.Functions.Like(u.SourceNote ?? "", "%copy%")
         orderby u.Last descending
         select new BatchWineType(u.Code, u.WineType)).FirstOrDefaultAsync();

    public Task<string?> FirstRealWineTypeAsync(string winery) =>
        (from b in db.Batches.AsNoTracking()
         join w in db.Wineries on b.WineryId equals w.Id
         where w.Code == winery && !EF.Functions.Like(b.SourceNote ?? "", "%copy%")
         orderby b.Code
         select b.WineType).FirstOrDefaultAsync();

    public Task<bool> LabelUsedAsync(long wineryId, long exceptVesselId, string label)
    {
        var lower = label.SqliteLower(trim: false);
        return db.Vessels.AnyAsync(v => v.WineryId == wineryId && v.Id != exceptVesselId && v.CellarLabel!.Trim().ToLower() == lower);
    }

    public async Task<long> InsertAsync(NewVessel v) => (await db.InsertAsync(new VesselEntity
    {
        WineryId = await db.Wineries.Where(w => w.Code == v.WineryCode).Select(w => (long?)w.Id).FirstOrDefaultAsync(),
        Code = v.Code, Type = v.Type, Name = v.Name, CalibrationJson = v.CalibrationJson, CapacityL = v.CapacityL, Source = v.Source,
        CellarLabel = v.CellarLabel, CellarLabelSource = v.CellarLabelSource, DepthCm = v.DepthCm, NeckDCm = v.NeckDCm, MaxDCm = v.MaxDCm, KnownVolumeL = v.KnownVolumeL,
    })).Id;

    public Task SetLabelAsync(long id, string? label, string? labelSource) => db.Vessels.Where(v => v.Id == id)
        .ExecuteUpdateAsync(u => u.SetProperty(v => v.CellarLabel, label).SetProperty(v => v.CellarLabelSource, labelSource)).GuardAsync();

    public Task SetMeasurementsAsync(long id, VesselMeasurements m) => db.Vessels.Where(v => v.Id == id).ExecuteUpdateAsync(u => u
        .SetProperty(v => v.DepthCm, m.DepthCm).SetProperty(v => v.NeckDCm, m.NeckDCm).SetProperty(v => v.MaxDCm, m.MaxDCm)
        .SetProperty(v => v.KnownVolumeL, m.KnownVolumeL).SetProperty(v => v.CalibrationJson, m.CalibrationJson)
        .SetProperty(v => v.CapacityL, m.CapacityL).SetProperty(v => v.Source, m.Source)).GuardAsync();

    /// Live-demo vessels that no operation, reading or lab row refers to.
    public Task DeleteUnusedLiveDemoAsync() => db.Vessels
        .Where(v => v.Source == "Entered (live demo)"
                    && !db.Operations.Any(o => o.FromVesselId == v.Id || o.ToVesselId == v.Id)
                    && !db.Readings.Any(r => r.VesselId == v.Id)
                    && !db.Lab.Any(l => l.VesselId == v.Id))
        .ExecuteDeleteAsync().GuardAsync();

    public Task ClearLiveDemoLabelsAsync() => db.Vessels.Where(v => v.CellarLabelSource == "Entered (live demo)")
        .ExecuteUpdateAsync(u => u.SetProperty(v => v.CellarLabel, (string?)null).SetProperty(v => v.CellarLabelSource, (string?)null)).GuardAsync();

    IQueryable<VesselWithWinery> Joined() =>
        db.Vessels.AsNoTracking().Join(db.Wineries, v => v.WineryId, w => w.Id, (v, w) => new VesselWithWinery { V = v, W = w });

    /// Batches that used the vessel (operations from/to, readings, lab), with the latest date.
    IQueryable<VesselUse> Uses(long vesselId)
    {
        var dates = db.Operations.Where(o => o.FromVesselId == vesselId || o.ToVesselId == vesselId).Select(o => new { o.BatchId, D = o.Date })
            .Concat(db.Readings.Where(r => r.VesselId == vesselId).Select(r => new { r.BatchId, D = r.TakenAt }))
            .Concat(db.Lab.Where(l => l.VesselId == vesselId).Select(l => new { l.BatchId, D = l.Date }));
        return from b in db.Batches.AsNoTracking()
               join x in dates on b.Id equals x.BatchId
               group x.D by new { b.Id, b.Code, b.SourceNote, b.WineType } into g
               select new VesselUse { Id = g.Key.Id, Code = g.Key.Code, SourceNote = g.Key.SourceNote, WineType = g.Key.WineType, Last = g.Max() };
    }

    sealed class VesselWithWinery
    {
        public VesselEntity V { get; init; } = null!;
        public WineryEntity W { get; init; } = null!;
    }

    sealed class VesselUse
    {
        public long Id { get; init; }
        public string? Code { get; init; }
        public string? SourceNote { get; init; }
        public string? WineType { get; init; }
        public string? Last { get; init; }
    }
}
