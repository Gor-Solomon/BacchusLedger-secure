using BacchusLedger.Application.Contracts.Batches;
using BacchusLedger.Application.Contracts.Readings;
using BacchusLedger.Application.Contracts.Report;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class EfLabRepository(BacchusLedgerDbContext db) : ILabRepository
{
    public Task<List<LabRowDto>> ListForBatchAsync(long batchId) =>
        (from l in db.Lab.AsNoTracking()
         from v in db.Vessels.Where(v => v.Id == l.VesselId).DefaultIfEmpty()
         where l.BatchId == batchId
         orderby l.Date, l.Id
         select new LabRowDto(l.Id, l.BatchId, l.Date, l.VesselId, l.Stage, l.SugarGl, l.TempC, l.Ph, l.TaGl, l.RsGl, l.Note, l.Source, v.Code)).ToListAsync();

    public Task<List<ReportLabRowDto>> ListForReportAsync(long batchId) =>
        (from l in db.Lab.AsNoTracking()
         from v in db.Vessels.Where(v => v.Id == l.VesselId).DefaultIfEmpty()
         where l.BatchId == batchId
         orderby l.Date, l.Id
         select new ReportLabRowDto(l.Id, l.Date, v.Code, l.Stage, l.SugarGl, l.TempC, l.Ph, l.TaGl, l.RsGl, l.Note, l.Source)).ToListAsync();

    /// The first lab sugar value of each batch (g/L, shown as is).
    public async Task<List<InitialSugarDto>> InitialSugarAsync(long? batchId, long? vesselId) =>
        (await (from l in db.Lab.AsNoTracking()
                join b in db.Batches on l.BatchId equals b.Id
                where l.SugarGl != null && (batchId == null || l.BatchId == batchId) && (vesselId == null || l.VesselId == vesselId)
                      && l.Date == db.Lab.Where(l2 => l2.BatchId == l.BatchId && l2.SugarGl != null).Min(l2 => l2.Date)
                orderby b.Code
                select new { l.Date, l.SugarGl, l.Source, b.Code }).ToListAsync())
        .Select(x => new InitialSugarDto(x.Date, x.SugarGl, x.Source, x.Code)).ToList();

    public Task InsertAsync(NewLabRow l) => db.InsertAsync(new LabEntity
    {
        BatchId = l.BatchId, Date = l.Date, VesselId = l.VesselId, Stage = l.Stage, SugarGl = l.SugarGl, TempC = l.TempC, Ph = l.Ph, TaGl = l.TaGl, RsGl = l.RsGl,
        Note = l.Note, Source = l.Source,
    });

    public Task<List<DatasetLabRow>> ListDatasetRowsAsync()
    {
        string[] sources = ["Dataset A3", "Dataset A4.1", "Dataset A4.2"];
        return db.Lab.AsNoTracking().Where(l => sources.Contains(l.Source)).OrderBy(l => l.Id)
            .Select(l => new DatasetLabRow(l.BatchId ?? 0, l.VesselId, l.Date!, l.Source!, l.Stage, l.Note, l.TempC, l.Ph, l.TaGl)).ToListAsync();
    }

    public Task DeleteForBatchAsync(long batchId) => db.Lab.Where(l => l.BatchId == batchId).ExecuteDeleteAsync().GuardAsync();
}
