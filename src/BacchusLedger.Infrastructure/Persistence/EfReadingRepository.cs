using BacchusLedger.Application.Contracts.Report;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class EfReadingRepository(BacchusLedgerDbContext db) : IReadingRepository
{
    public Task<List<ReadingRecord>> ListAsync(long? batchId, long? vesselId) =>
        (from r in db.Readings.AsNoTracking()
         join b in db.Batches on r.BatchId equals b.Id
         join w in db.Wineries on b.WineryId equals w.Id
         from v in db.Vessels.Where(v => v.Id == r.VesselId).DefaultIfEmpty()
         where (batchId == null || r.BatchId == batchId) && (vesselId == null || r.VesselId == vesselId)
         orderby r.TakenAt, r.Id
         select new ReadingRecord(r.Id, r.BatchId, r.VesselId, r.TakenAt, r.Type!, r.Value!.Value, r.Unit, r.Instrument, r.Person, r.Note, r.Source,
             b.Code, v.Code, v.CellarLabel, w.Code)).ToListAsync();

    public Task<List<ReportReadingDto>> ListForReportAsync(long batchId) =>
        (from r in db.Readings.AsNoTracking()
         from v in db.Vessels.Where(v => v.Id == r.VesselId).DefaultIfEmpty()
         where r.BatchId == batchId
         orderby r.TakenAt, r.Id
         select new ReportReadingDto(r.Id, r.TakenAt, v.Code, r.Type, r.Value, r.Unit, r.Instrument, r.Person, r.Note, r.Source)).ToListAsync();

    public Task<List<LatestReading>> ListBasicAsync(long batchId)
    {
        string[] types = ["temperature", "ph", "ta"];
        return (from r in db.Readings.AsNoTracking()
                from v in db.Vessels.Where(v => v.Id == r.VesselId).DefaultIfEmpty()
                where r.BatchId == batchId && types.Contains(r.Type)
                orderby r.TakenAt, r.Id
                select new LatestReading(r.Type!, r.Value, r.Unit, r.TakenAt!, r.Source, v.Code, v.CellarLabel)).ToListAsync();
    }

    public async Task<long> InsertAsync(NewReading r) => (await db.InsertAsync(new ReadingEntity
    {
        BatchId = r.BatchId, VesselId = r.VesselId, TakenAt = r.TakenAt, Type = r.Type, Value = r.Value, Unit = r.Unit, Instrument = r.Instrument,
        Person = r.Person, Note = r.Note, Source = r.Source,
    })).Id;

    /// The earliest of: a non-Brix reading of the vessel/batch, or a lab row (its date at 09:00), up to takenAt.
    public async Task<string?> FirstFermentationAsync(long batchId, long? vesselId, string takenAt)
    {
        var reading = await db.Readings.Where(r => r.BatchId == batchId && r.VesselId == vesselId && r.Type != "initial_brix" && string.Compare(r.TakenAt, takenAt) <= 0)
            .MinAsync(r => r.TakenAt);
        var lab = await db.Lab.Where(l => l.BatchId == batchId && l.VesselId == vesselId && string.Compare(l.Date + "T09:00", takenAt) <= 0)
            .MinAsync(l => l.Date + "T09:00");
        return reading == null ? lab : lab == null ? reading : string.CompareOrdinal(reading, lab) <= 0 ? reading : lab;
    }

    public Task<bool> HasInitialBrixAsync(long batchId, long? vesselId) =>
        db.Readings.AnyAsync(r => r.BatchId == batchId && r.VesselId == vesselId && r.Type == "initial_brix");

    public Task<double?> LatestBrixAsync(long batchId, long? vesselId) =>
        db.Readings.Where(r => r.BatchId == batchId && r.VesselId == vesselId && r.Type == "initial_brix").OrderByDescending(r => r.Id).Select(r => r.Value).FirstOrDefaultAsync();

    public Task<double?> LastValueAsync(long vesselId, string type) =>
        db.Readings.Where(r => r.VesselId == vesselId && r.Type == type).OrderByDescending(r => r.TakenAt).ThenByDescending(r => r.Id).Select(r => r.Value).FirstOrDefaultAsync();

    public Task<bool> VesselHasInitialBrixAsync(string vesselCode) =>
        db.Readings.AnyAsync(r => r.Type == "initial_brix" && db.Vessels.Any(v => v.Id == r.VesselId && v.Code == vesselCode));

    public Task<long> CountAsync(string batchCode, string type, string source) =>
        db.Readings.LongCountAsync(r => r.Type == type && r.Source == source && db.Batches.Any(b => b.Id == r.BatchId && b.Code == batchCode));

    public Task<long> CountInventedAsync(IReadOnlyList<string> seededBatchCodes)
    {
        string[] types = ["initial_brix", "hydrometer_density", "hydrometer_abv"];
        return (from r in db.Readings
                join b in db.Batches on r.BatchId equals b.Id
                where types.Contains(r.Type) && (seededBatchCodes.Contains(b.Code) || !EF.Functions.Like(r.Source, "Entered%"))
                select r).LongCountAsync();
    }

    public Task<long> CountFromDatasetAsync() => db.Readings.LongCountAsync(r => EF.Functions.Like(r.Source, "Dataset%"));

    public Task DeleteForBatchAsync(long batchId) => db.Readings.Where(r => r.BatchId == batchId).ExecuteDeleteAsync().GuardAsync();
}
