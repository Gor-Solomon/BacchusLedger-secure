using BacchusLedger.Contracts.Report;
using BacchusLedger.Models;
using BacchusLedger.Repositories;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class SqliteReadingRepository(ISqliteConnectionFactory connections) : IReadingRepository
{
    public async Task<List<ReadingRecord>> ListAsync(long? batchId, long? vesselId)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("""
            SELECT r.*, b.code AS batch_code, v.code AS vessel_code, v.cellar_label, w.code AS winery
            FROM readings r JOIN batches b ON b.id=r.batch_id JOIN wineries w ON w.id=b.winery_id LEFT JOIN vessels v ON v.id=r.vessel_id
            WHERE ($1 IS NULL OR r.batch_id=$1) AND ($2 IS NULL OR r.vessel_id=$2) ORDER BY r.taken_at, r.id
            """, r => new ReadingRecord(r.Long("id"), r.LongOrNull("batch_id"), r.LongOrNull("vessel_id"), r.Str("taken_at"), r.Str("type")!, r.Dbl("value")!.Value,
            r.Str("unit"), r.Str("instrument"), r.Str("person"), r.Str("note"), r.Str("source"), r.Str("batch_code"), r.Str("vessel_code"), r.Str("cellar_label"),
            r.Str("winery")), batchId, vesselId);
    }

    public async Task<List<ReportReadingDto>> ListForReportAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("""
            SELECT r.id, r.taken_at, v.code AS vessel, r.type, r.value, r.unit, r.instrument, r.person, r.note, r.source FROM readings r
            LEFT JOIN vessels v ON v.id=r.vessel_id
            WHERE r.batch_id=$1 ORDER BY r.taken_at, r.id
            """, r => new ReportReadingDto(r.Long("id"), r.Str("taken_at"), r.Str("vessel"), r.Str("type"), r.Dbl("value"), r.Str("unit"), r.Str("instrument"),
            r.Str("person"), r.Str("note"), r.Str("source")), batchId);
    }

    public async Task<List<LatestReading>> ListBasicAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("""
            SELECT r.type, r.value, r.unit, r.taken_at, r.source, v.code AS vessel, v.cellar_label FROM readings r LEFT JOIN vessels v ON v.id=r.vessel_id
            WHERE r.batch_id=$1 AND r.type IN ('temperature','ph','ta') ORDER BY r.taken_at, r.id
            """, r => new LatestReading(r.Str("type")!, r.Dbl("value"), r.Str("unit"), r.Str("taken_at")!, r.Str("source"), r.Str("vessel"), r.Str("cellar_label")), batchId);
    }

    public async Task<long> InsertAsync(NewReading r)
    {
        await using var db = await connections.OpenAsync();
        return await db.InsertAsync("INSERT INTO readings(batch_id,vessel_id,taken_at,type,value,unit,instrument,person,note,source) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)",
            r.BatchId, r.VesselId, r.TakenAt, r.Type, r.Value, r.Unit, r.Instrument, r.Person, r.Note, r.Source);
    }

    public async Task<string?> FirstFermentationAsync(long batchId, long? vesselId, string takenAt)
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync($"""
            SELECT MIN(t) t FROM (
              SELECT taken_at AS t FROM readings WHERE batch_id=$1 AND {VesselClause(vesselId)} AND type<>'initial_brix' AND taken_at<=$3
              UNION ALL SELECT date || 'T09:00' FROM lab WHERE batch_id=$1 AND {VesselClause(vesselId)} AND date || 'T09:00'<=$3)
            """, r => r.Str("t"), batchId, vesselId, takenAt))[0];
    }

    public async Task<bool> HasInitialBrixAsync(long batchId, long? vesselId)
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync($"SELECT 1 x FROM readings WHERE batch_id=$1 AND {VesselClause(vesselId)} AND type='initial_brix'", r => 1, batchId, vesselId)).Count > 0;
    }

    public async Task<double?> LatestBrixAsync(long batchId, long? vesselId)
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync($"SELECT value FROM readings WHERE batch_id=$1 AND {VesselClause(vesselId)} AND type='initial_brix' ORDER BY id DESC LIMIT 1",
            r => r.Dbl("value"), batchId, vesselId)).FirstOrDefault();
    }

    public async Task<double?> LastValueAsync(long vesselId, string type)
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync("SELECT value FROM readings WHERE vessel_id=$1 AND type=$2 ORDER BY taken_at DESC, id DESC LIMIT 1", r => r.Dbl("value"), vesselId, type))
            .FirstOrDefault();
    }

    public async Task<bool> VesselHasInitialBrixAsync(string vesselCode)
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync("SELECT 1 x FROM readings r JOIN vessels v ON v.id=r.vessel_id WHERE v.code=$1 AND r.type='initial_brix'", r => 1, vesselCode)).Count > 0;
    }

    public async Task<long> CountAsync(string batchCode, string type, string source)
    {
        await using var db = await connections.OpenAsync();
        return await db.CountAsync("SELECT COUNT(*) n FROM readings r JOIN batches b ON b.id=r.batch_id WHERE b.code=$1 AND r.type=$2 AND r.source=$3", batchCode, type, source);
    }

    public async Task<long> CountInventedAsync(IReadOnlyList<string> seededBatchCodes)
    {
        await using var db = await connections.OpenAsync();
        var codes = string.Join(",", seededBatchCodes.Select((_, i) => "$" + (i + 1)));
        return await db.CountAsync($"""
            SELECT COUNT(*) n FROM readings r JOIN batches b ON b.id=r.batch_id
            WHERE r.type IN ('initial_brix','hydrometer_density','hydrometer_abv')
              AND (b.code IN ({codes}) OR r.source NOT LIKE 'Entered%')
            """, seededBatchCodes.Cast<object?>().ToArray());
    }

    public async Task<long> CountFromDatasetAsync()
    {
        await using var db = await connections.OpenAsync();
        return await db.CountAsync("SELECT COUNT(*) FROM readings WHERE source LIKE 'Dataset%'");
    }

    public async Task DeleteForBatchAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync("DELETE FROM readings WHERE batch_id=$1", batchId);
    }

    static string VesselClause(long? vesselId) => vesselId == null ? "vessel_id IS NULL" : "vessel_id=$2";
}
