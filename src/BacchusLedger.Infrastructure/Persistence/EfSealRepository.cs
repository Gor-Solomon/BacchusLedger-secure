using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class EfSealRepository(BacchusLedgerDbContext db) : ISealRepository
{
    public Task<string?> GetVersionAsync() => db.SealMeta.AsNoTracking().Where(m => m.Key == "version").Select(m => m.Value).FirstOrDefaultAsync();

    /// Insert or replace (as INSERT OR REPLACE did).
    public async Task SetVersionAsync(string version)
    {
        if (await db.SealMeta.Where(m => m.Key == "version").ExecuteUpdateAsync(u => u.SetProperty(m => m.Value, version)).GuardAsync() == 0)
            await db.InsertAsync(new SealMetaEntity { Key = "version", Value = version });
    }

    public async Task ClearAllAsync()
    {
        await db.SealChain.ExecuteDeleteAsync().GuardAsync();
        await db.SealBatches.ExecuteDeleteAsync().GuardAsync();
        await db.SealTamper.ExecuteDeleteAsync().GuardAsync();
    }

    public Task<List<SealChainEntry>> ChainAsync(long batchId) =>
        db.SealChain.AsNoTracking().Where(c => c.BatchId == batchId).OrderBy(c => c.Seq)
            .Select(c => new SealChainEntry(c.Seq, c.Tbl!, c.RowId ?? 0, c.PrevHash!, c.Hash!)).ToListAsync();

    public Task<SealRecord?> GetAsync(long batchId) =>
        db.SealBatches.AsNoTracking().Where(s => s.BatchId == batchId)
            .Select(s => new SealRecord(s.BatchId, s.BatchCode!, s.FinalHash!, s.Signature!, s.SealedAt, s.Entries, s.KeyId)).FirstOrDefaultAsync();

    /// Chain order: vineyard deliveries first, then operations, lab, readings (each by id).
    public async Task<List<SealEntryRef>> CurrentEntriesAsync(long batchId)
    {
        var list = (await db.Deliveries.AsNoTracking().Where(d => db.BatchOrigins.Any(bo => bo.DeliveryId == d.Id && bo.BatchId == batchId))
            .OrderBy(d => d.Id).Select(d => d.Id).ToListAsync()).Select(id => new SealEntryRef("deliveries", id)).ToList();
        list.AddRange((await db.Operations.AsNoTracking().Where(o => o.BatchId == batchId).OrderBy(o => o.Id).Select(o => o.Id).ToListAsync()).Select(id => new SealEntryRef("operations", id)));
        list.AddRange((await db.Lab.AsNoTracking().Where(l => l.BatchId == batchId).OrderBy(l => l.Id).Select(l => l.Id).ToListAsync()).Select(id => new SealEntryRef("lab", id)));
        list.AddRange((await db.Readings.AsNoTracking().Where(r => r.BatchId == batchId).OrderBy(r => r.Id).Select(r => r.Id).ToListAsync()).Select(id => new SealEntryRef("readings", id)));
        return list;
    }

    /// Every column of the row under its database column name, with the same value types as the database gives (INTEGER → long, REAL → double, TEXT → string),
    /// so the hash of an old sealed row does not change.
    public async Task<Dictionary<string, object?>?> GetRowAsync(string table, long id)
    {
        object? entity = table switch
        {
            "deliveries" => await db.Deliveries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id),
            "operations" => await db.Operations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id),
            "lab" => await db.Lab.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id),
            "readings" => await db.Readings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id),
            _ => throw new ArgumentException($"not a sealed table: {table}"),
        };
        if (entity == null) return null;
        var type = db.Model.FindEntityType(entity.GetType())!;
        return type.GetProperties().ToDictionary(p => p.GetColumnName(), p => p.PropertyInfo!.GetValue(entity));
    }

    public Task AddEntryAsync(long batchId, long seq, string table, long rowId, string prev, string hash) =>
        db.InsertAsync(new SealChainEntity { BatchId = batchId, Seq = seq, Tbl = table, RowId = rowId, PrevHash = prev, Hash = hash });

    /// Insert or replace (as INSERT OR REPLACE did).
    public async Task SaveAsync(SealRecord s)
    {
        await db.SealBatches.Where(x => x.BatchId == s.BatchId).ExecuteDeleteAsync().GuardAsync();
        await db.InsertAsync(new SealBatchEntity
        {
            BatchId = s.BatchId, BatchCode = s.BatchCode, FinalHash = s.FinalHash, Signature = s.Signature, SealedAt = s.SealedAt, Entries = s.Entries, KeyId = s.KeyId,
        });
    }

    public async Task DeleteChainAndSealAsync(long batchId)
    {
        await db.SealChain.Where(c => c.BatchId == batchId).ExecuteDeleteAsync().GuardAsync();
        await db.SealBatches.Where(s => s.BatchId == batchId).ExecuteDeleteAsync().GuardAsync();
    }

    public Task DeleteTamperRecordsAsync(long batchId) => db.SealTamper.Where(t => t.BatchId == batchId).ExecuteDeleteAsync().GuardAsync();

    /// Newest first (rowid).
    public Task<List<TamperRecord>> TamperRecordsAsync(long batchId) =>
        db.SealTamper.AsNoTracking().Where(t => t.BatchId == batchId).OrderByDescending(t => t.Rowid).Select(t => new TamperRecord(t.RowId ?? 0, t.OldValue)).ToListAsync();

    public Task<SealedLabTemperature?> FirstSealedLabTemperatureAsync(long batchId) =>
        (from s in db.SealChain.AsNoTracking()
         join l in db.Lab on s.RowId equals l.Id
         where s.BatchId == batchId && s.Tbl == "lab" && l.TempC != null
         orderby s.Seq
         select new SealedLabTemperature(s.Seq, l.Id, l.Date, l.TempC!.Value)).FirstOrDefaultAsync();

    public Task SetLabTemperatureAsync(long labId, double? value) =>
        db.Lab.Where(l => l.Id == labId).ExecuteUpdateAsync(u => u.SetProperty(l => l.TempC, value)).GuardAsync();

    public Task<bool> TamperRecordedAsync(long labId) => db.SealTamper.AnyAsync(t => t.Tbl == "lab" && t.RowId == labId);

    public Task AddTamperRecordAsync(long labId, double oldValue, long batchId) =>
        db.InsertAsync(new SealTamperEntity { Tbl = "lab", RowId = labId, Col = "temp_c", OldValue = oldValue, BatchId = batchId });
}
