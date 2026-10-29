using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Domain.Losses;
using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class EfOperationRepository(BacchusLedgerDbContext db) : IOperationRepository
{
    public Task<List<CellarOperation>> ListForBatchAsync(long batchId) =>
        (from o in db.Operations.AsNoTracking()
         where o.BatchId == batchId
         from vf in db.Vessels.Where(v => v.Id == o.FromVesselId).DefaultIfEmpty()
         from vt in db.Vessels.Where(v => v.Id == o.ToVesselId).DefaultIfEmpty()
         orderby o.Date, o.Id
         select new CellarOperation
         {
             Id = o.Id, Date = o.Date!, Type = o.Type!, FromId = o.FromVesselId, ToId = o.ToVesselId,
             FromCode = vf.Code, ToCode = vt.Code, FromLabel = vf.CellarLabel, ToLabel = vt.CellarLabel,
             Litres = o.Litres, Bottles = (int?)o.Bottles, Ml = o.BottleMl, Remainder = o.RemainderL, Dip = o.DipCm,
             Method = o.Method, Note = o.Note, Source = o.Source, Person = o.Person,
         }).ToListAsync();

    public async Task<long> InsertAsync(NewOperation o) => (await db.InsertAsync(new OperationEntity
    {
        BatchId = o.BatchId, Date = o.Date, Type = o.Type, FromVesselId = o.FromVesselId, ToVesselId = o.ToVesselId, Litres = o.Litres, Bottles = o.Bottles,
        BottleMl = o.BottleMl, RemainderL = o.RemainderL, Method = o.Method, DipCm = o.DipCm, Person = o.Person, Note = o.Note, Source = o.Source, CreatedAt = o.CreatedAt,
    })).Id;

    public Task<string?> FirstDateAsync(long batchId) => db.Operations.Where(o => o.BatchId == batchId).MinAsync(o => o.Date);

    public Task<bool> AnyRecordedSinceAsync(long vesselId, string sinceIso) =>
        db.Operations.AnyAsync(o => (o.FromVesselId == vesselId || o.ToVesselId == vesselId) && string.Compare(o.CreatedAt, sinceIso) >= 0);

    public Task DeleteForBatchAsync(long batchId) => db.Operations.Where(o => o.BatchId == batchId).ExecuteDeleteAsync().GuardAsync();
}
