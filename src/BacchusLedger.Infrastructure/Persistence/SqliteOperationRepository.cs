using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Domain.Losses;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class SqliteOperationRepository(ISqliteConnectionFactory connections) : IOperationRepository
{
    public async Task<List<CellarOperation>> ListForBatchAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("""
            SELECT o.*, vf.code AS from_code, vt.code AS to_code, vf.cellar_label AS from_label, vt.cellar_label AS to_label FROM operations o
            LEFT JOIN vessels vf ON vf.id=o.from_vessel_id LEFT JOIN vessels vt ON vt.id=o.to_vessel_id
            WHERE o.batch_id=$1 ORDER BY o.date, o.id
            """, r => new CellarOperation
        {
            Id = r.Long("id"), Date = r.Str("date")!, Type = r.Str("type")!,
            FromId = r.LongOrNull("from_vessel_id"), ToId = r.LongOrNull("to_vessel_id"),
            FromCode = r.Str("from_code"), ToCode = r.Str("to_code"), FromLabel = r.Str("from_label"), ToLabel = r.Str("to_label"),
            Litres = r.Dbl("litres"), Bottles = r.LongOrNull("bottles") is { } b ? (int)b : null,
            Ml = r.Dbl("bottle_ml"), Remainder = r.Dbl("remainder_l"), Dip = r.Dbl("dip_cm"),
            Method = r.Str("method"), Note = r.Str("note"), Source = r.Str("source"), Person = r.Str("person"),
        }, batchId);
    }

    public async Task<long> InsertAsync(NewOperation o)
    {
        await using var db = await connections.OpenAsync();
        return await db.InsertAsync(
            "INSERT INTO operations(batch_id,date,type,from_vessel_id,to_vessel_id,litres,bottles,bottle_ml,remainder_l,method,dip_cm,person,note,source,created_at) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)",
            o.BatchId, o.Date, o.Type, o.FromVesselId, o.ToVesselId, o.Litres, o.Bottles, o.BottleMl, o.RemainderL, o.Method, o.DipCm, o.Person, o.Note, o.Source, o.CreatedAt);
    }

    public async Task<string?> FirstDateAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync("SELECT MIN(date) d FROM operations WHERE batch_id=$1", r => r.Str("d"), batchId))[0];
    }

    public async Task<bool> AnyRecordedSinceAsync(long vesselId, string sinceIso)
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync("SELECT 1 x FROM operations WHERE (from_vessel_id=$1 OR to_vessel_id=$1) AND created_at>=$2", r => 1, vesselId, sinceIso)).Count > 0;
    }

    public async Task DeleteForBatchAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync("DELETE FROM operations WHERE batch_id=$1", batchId);
    }
}
