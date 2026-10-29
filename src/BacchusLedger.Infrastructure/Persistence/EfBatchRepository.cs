using System.Linq.Expressions;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class EfBatchRepository(BacchusLedgerDbContext db) : IBatchRepository
{
    static readonly Expression<Func<BatchWithWinery, BatchRecord>> ToRecord = x => new BatchRecord(x.B.Id, x.B.Code!, x.B.Name, x.B.WineType, x.B.Technology,
        x.B.Variety, x.B.Vintage, x.B.Abv, x.B.Status, x.B.SourceNote, x.B.GrapesKg, x.W.Code!, x.W.Name);

    public Task<BatchRecord?> GetAsync(long id) => Joined().Where(x => x.B.Id == id).Select(ToRecord).FirstOrDefaultAsync();

    public Task<BatchRecord?> GetByCodeAsync(string code) => Joined().Where(x => x.B.Code == code).Select(ToRecord).FirstOrDefaultAsync();

    public Task<long?> FindIdAsync(string code) => db.Batches.AsNoTracking().Where(b => b.Code == code).Select(b => (long?)b.Id).FirstOrDefaultAsync();

    public Task<List<BatchRecord>> ListAsync() => Joined().OrderBy(x => x.B.Code).Select(ToRecord).ToListAsync();

    public Task<List<BatchRecord>> ListRealAsync() => Joined().Where(x => !EF.Functions.Like(x.B.SourceNote ?? "", "%copy%")).Select(ToRecord).ToListAsync();

    public Task<List<long>> ListRealIdsAsync() =>
        db.Batches.AsNoTracking().Where(b => !EF.Functions.Like(b.SourceNote ?? "", "%copy%")).Select(b => b.Id).ToListAsync();

    public Task<List<BatchRef>> ListRefsAsync() =>
        (from b in db.Batches.AsNoTracking()
         join w in db.Wineries on b.WineryId equals w.Id
         select new BatchRef(b.Id, b.Code!, b.SourceNote, w.Code!)).ToListAsync();

    public Task<List<long>> ListIdsAsync() => db.Batches.AsNoTracking().OrderBy(b => b.Id).Select(b => b.Id).ToListAsync();

    public Task<List<long>> ListIdsAsync(long? batch, string? winery)
    {
        var q = db.Batches.AsNoTracking();
        if (batch != null) q = q.Where(b => b.Id == batch);
        if (!string.IsNullOrEmpty(winery)) q = q.Where(b => db.Wineries.Any(w => w.Id == b.WineryId && w.Code == winery));
        return q.OrderBy(b => b.Code).Select(b => b.Id).ToListAsync();
    }

    public Task<long> CountCodesLikeAsync(string pattern) => db.Batches.LongCountAsync(b => EF.Functions.Like(b.Code!, pattern));

    public async Task<long> InsertAsync(NewBatch b) => (await db.InsertAsync(new BatchEntity
    {
        WineryId = await db.Wineries.Where(w => w.Code == b.WineryCode).Select(w => (long?)w.Id).FirstOrDefaultAsync(),
        Code = b.Code, Name = b.Name, WineType = b.WineType, Technology = b.Technology, Variety = b.Variety, Vintage = b.Vintage, Abv = b.Abv,
        Status = b.Status, SourceNote = b.SourceNote, GrapesKg = b.GrapesKg,
    })).Id;

    public Task SetStatusAsync(string code, string status) =>
        db.Batches.Where(b => b.Code == code).ExecuteUpdateAsync(u => u.SetProperty(b => b.Status, status)).GuardAsync();

    public Task DeleteAsync(long id) => db.Batches.Where(b => b.Id == id).ExecuteDeleteAsync().GuardAsync();

    IQueryable<BatchWithWinery> Joined() =>
        db.Batches.AsNoTracking().Join(db.Wineries, b => b.WineryId, w => w.Id, (b, w) => new BatchWithWinery { B = b, W = w });

    sealed class BatchWithWinery
    {
        public BatchEntity B { get; init; } = null!;
        public WineryEntity W { get; init; } = null!;
    }
}
