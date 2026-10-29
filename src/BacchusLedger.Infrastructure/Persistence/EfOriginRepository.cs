using BacchusLedger.Application.Contracts.Batches;
using BacchusLedger.Application.Contracts.Origins;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Domain.Ledger;
using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

/// Sums and batch lists are fetched as one grouped query each and joined in memory (no query per row).
/// A farmer or plot without deliveries keeps kg_delivered = null, and a delivery without a batch keeps batches = null, as before.
public sealed class EfOriginRepository(BacchusLedgerDbContext db) : IOriginRepository
{
    public async Task<List<FarmerDto>> ListFarmersAsync()
    {
        var plots = await db.Plots.AsNoTracking().GroupBy(p => p.FarmerId).Select(g => new { g.Key, N = g.LongCount() }).ToDictionaryAsync(x => x.Key ?? 0, x => x.N);
        var kg = await db.Deliveries.AsNoTracking().GroupBy(d => d.FarmerId).Select(g => new { g.Key, Kg = g.Sum(d => d.Kg) }).ToDictionaryAsync(x => x.Key ?? 0, x => x.Kg);
        return (await db.Farmers.AsNoTracking().OrderBy(f => f.Code).ToListAsync())
            .Select(f => new FarmerDto(f.Id, f.Code, f.Name, f.Phone, f.IsDemo, plots.GetValueOrDefault(f.Id), kg.GetValueOrDefault(f.Id))).ToList();
    }

    public Task<long> CountFarmersAsync() => db.Farmers.LongCountAsync();

    public Task<long?> FindFarmerIdAsync(string code) => db.Farmers.Where(f => f.Code == code).Select(f => (long?)f.Id).FirstOrDefaultAsync();

    public async Task<long> InsertFarmerAsync(string code, string name, string? phone, bool isDemo) =>
        (await db.InsertAsync(new FarmerEntity { Code = code, Name = name, Phone = phone, IsDemo = isDemo ? 1 : 0 })).Id;

    public async Task<List<PlotDto>> ListPlotsAsync()
    {
        var kg = await db.Deliveries.AsNoTracking().GroupBy(d => d.PlotId).Select(g => new { g.Key, Kg = g.Sum(d => d.Kg) }).ToDictionaryAsync(x => x.Key ?? 0, x => x.Kg);
        var rows = await (from p in db.Plots.AsNoTracking()
                          join f in db.Farmers on p.FarmerId equals f.Id
                          from w in db.Wineries.Where(w => w.Id == p.WineryId).DefaultIfEmpty()
                          orderby p.Code
                          select new { p, Farmer = f.Name, Winery = w.Code }).ToListAsync();
        return rows.Select(x => new PlotDto(x.p.Id, x.p.FarmerId, x.p.WineryId, x.p.Code, x.p.CadastreNo, x.p.Village, x.p.Municipality, x.p.Region, x.p.AreaHa,
            x.p.Variety, x.p.Lat, x.p.Lng, x.p.LicenceNo, x.p.SoilTexture, x.p.SoilPh, x.p.RainMm, x.p.SummerTempC, x.p.IsDemo, x.Farmer, x.Winery,
            kg.GetValueOrDefault(x.p.Id), false)).ToList();
    }

    public Task<long?> FindPlotIdAsync(string code) => db.Plots.Where(p => p.Code == code).Select(p => (long?)p.Id).FirstOrDefaultAsync();

    public async Task<long> InsertPlotAsync(NewPlot p) => (await db.InsertAsync(new PlotEntity
    {
        FarmerId = p.FarmerId, WineryId = await db.Wineries.Where(w => w.Code == p.WineryCode).Select(w => (long?)w.Id).FirstOrDefaultAsync(),
        Code = p.Code, CadastreNo = p.CadastreNo, Village = p.Village, Municipality = p.Municipality, Region = p.Region, AreaHa = p.AreaHa, Variety = p.Variety,
        Lat = p.Lat, Lng = p.Lng, LicenceNo = p.LicenceNo, SoilTexture = p.SoilTexture, SoilPh = p.SoilPh, RainMm = p.RainMm, SummerTempC = p.SummerTempC, IsDemo = p.IsDemo,
    })).Id;

    public async Task<long?> GetPlotFarmerIdAsync(long plotId) =>
        (await db.Plots.AsNoTracking().Where(p => p.Id == plotId).Select(p => new { p.FarmerId }).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"plot {plotId} not found")).FarmerId;

    public async Task<long> InsertOrderAsync(NewOrder o) => (await db.InsertAsync(new OrderEntity
    {
        WineryId = await db.Wineries.Where(w => w.Code == o.WineryCode).Select(w => (long?)w.Id).FirstOrDefaultAsync(),
        FarmerId = o.FarmerId, PlotId = o.PlotId, Variety = o.Variety, KgPlanned = o.KgPlanned, PriceGelPerKg = o.PriceGelPerKg, Date = o.Date, IsDemo = 1, Source = o.Source,
    })).Id;

    public Task DeleteOrderAsync(long id) => db.Orders.Where(o => o.Id == id).ExecuteDeleteAsync().GuardAsync();

    public async Task<List<DeliveryDto>> ListDeliveriesAsync()
    {
        var batches = await BatchCodesByDeliveryAsync();
        var rows = await (from d in db.Deliveries.AsNoTracking()
                          join f in db.Farmers on d.FarmerId equals f.Id
                          join p in db.Plots on d.PlotId equals p.Id
                          from w in db.Wineries.Where(w => w.Id == p.WineryId).DefaultIfEmpty()
                          orderby d.Date, d.Id
                          select new { d, Farmer = f.Name, Plot = p.Code, Winery = w.Code }).ToListAsync();
        return rows.Select(x => new DeliveryDto(x.d.Id, x.d.OrderId, x.d.PlotId, x.d.FarmerId, x.d.Date, x.d.Kg, x.d.SugarGl, x.d.QualityNote, x.d.Accepted,
            x.d.Source, x.Farmer, x.Plot, x.Winery, batches.GetValueOrDefault(x.d.Id))).ToList();
    }

    public async Task<long> InsertDeliveryAsync(NewDelivery d) => (await db.InsertAsync(new DeliveryEntity
    {
        OrderId = d.OrderId, PlotId = d.PlotId, FarmerId = d.FarmerId, Date = d.Date, Kg = d.Kg, SugarGl = d.SugarGl, QualityNote = d.QualityNote,
        Accepted = d.Accepted, Source = d.Source,
    })).Id;

    public async Task<double?> GetDeliveryKgAsync(long id) =>
        (await db.Deliveries.AsNoTracking().Where(d => d.Id == id).Select(d => new { d.Kg }).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"delivery {id} not found")).Kg;

    public Task DeleteDeliveryAsync(long id) => db.Deliveries.Where(d => d.Id == id).ExecuteDeleteAsync().GuardAsync();

    public Task InsertBatchOriginAsync(long batchId, long deliveryId, double? kg) =>
        db.InsertAsync(new BatchOriginEntity { BatchId = batchId, DeliveryId = deliveryId, Kg = kg });

    public Task DeleteBatchOriginsAsync(long batchId) => db.BatchOrigins.Where(bo => bo.BatchId == batchId).ExecuteDeleteAsync().GuardAsync();

    public Task<List<BatchOriginDto>> ListBatchOriginsAsync(long batchId) =>
        (from bo in db.BatchOrigins.AsNoTracking()
         join b in db.Batches on bo.BatchId equals b.Id
         join d in db.Deliveries on bo.DeliveryId equals d.Id
         join f in db.Farmers on d.FarmerId equals f.Id
         join p in db.Plots on d.PlotId equals p.Id
         where bo.BatchId == batchId
         orderby d.Id
         select new BatchOriginDto(bo.Kg, Math.Round(bo.Kg!.Value * 100.0 / b.GrapesKg!.Value, 3), d.Id, d.Date, d.SugarGl, d.Source, f.Name, p.Code, p.AreaHa,
             p.Variety, p.Municipality, p.Region, p.CadastreNo, p.Lat, p.Lng, p.SoilPh, p.SoilTexture, p.RainMm, p.SummerTempC)).ToListAsync();

    public Task<List<OriginCheckRow>> ListOriginChecksAsync(long batchId) =>
        (from bo in db.BatchOrigins.AsNoTracking()
         join d in db.Deliveries on bo.DeliveryId equals d.Id
         join p in db.Plots on d.PlotId equals p.Id
         where bo.BatchId == batchId
         select new OriginCheckRow(bo.Kg, p.Code, p.Municipality)).ToListAsync();

    public async Task<Dictionary<long, List<OriginCheckRow>>> ListOriginChecksAsync(IReadOnlyCollection<long> batchIds) =>
        (await (from bo in db.BatchOrigins.AsNoTracking()
                join d in db.Deliveries on bo.DeliveryId equals d.Id
                join p in db.Plots on d.PlotId equals p.Id
                where batchIds.Contains(bo.BatchId ?? 0)
                orderby bo.Rowid
                select new { BatchId = bo.BatchId ?? 0, bo.Kg, p.Code, p.Municipality }).ToListAsync())
        .GroupBy(x => x.BatchId).ToDictionary(g => g.Key, g => g.Select(x => new OriginCheckRow(x.Kg, x.Code, x.Municipality)).ToList());

    public Task<List<HarvestDelivery>> ListLedgerOriginsAsync(long batchId) =>
        (from bo in db.BatchOrigins.AsNoTracking()
         join d in db.Deliveries on bo.DeliveryId equals d.Id
         join p in db.Plots on d.PlotId equals p.Id
         from f in db.Farmers.Where(f => f.Id == d.FarmerId).DefaultIfEmpty()
         where bo.BatchId == batchId
         orderby d.Date, d.Id
         select new HarvestDelivery(d.Id, d.Date!, bo.Kg, d.Source, p.Code, p.Municipality, f.Name)).ToListAsync();

    public Task<List<FarmerOrigin>> ListFarmerOriginsAsync(long batchId) =>
        (from bo in db.BatchOrigins.AsNoTracking()
         join d in db.Deliveries on bo.DeliveryId equals d.Id
         join f in db.Farmers on d.FarmerId equals f.Id
         join p in db.Plots on d.PlotId equals p.Id
         where bo.BatchId == batchId
         orderby d.Id
         select new FarmerOrigin(f.Name, p.Code, p.Municipality, bo.Kg, d.Source)).ToListAsync();

    public Task<List<OriginLink>> ListOriginLinksAsync(string batchCode) =>
        (from bo in db.BatchOrigins.AsNoTracking()
         join b in db.Batches on bo.BatchId equals b.Id
         where b.Code == batchCode
         select new OriginLink(bo.DeliveryId ?? 0, bo.Kg)).ToListAsync();

    /// Batch codes per delivery, comma separated in batch_origins order (as group_concat gave them).
    async Task<Dictionary<long, string>> BatchCodesByDeliveryAsync() =>
        (await (from bo in db.BatchOrigins.AsNoTracking()
                join b in db.Batches on bo.BatchId equals b.Id
                orderby bo.Rowid
                select new { bo.DeliveryId, b.Code }).ToListAsync())
        .GroupBy(x => x.DeliveryId ?? 0).ToDictionary(g => g.Key, g => string.Join(",", g.Select(x => x.Code)));
}
