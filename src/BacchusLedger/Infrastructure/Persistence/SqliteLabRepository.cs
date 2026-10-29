using BacchusLedger.Contracts.Batches;
using BacchusLedger.Contracts.Readings;
using BacchusLedger.Contracts.Report;
using BacchusLedger.Models;
using BacchusLedger.Repositories;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class SqliteLabRepository(ISqliteConnectionFactory connections) : ILabRepository
{
    public async Task<List<LabRowDto>> ListForBatchAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("SELECT l.*, v.code AS vessel FROM lab l LEFT JOIN vessels v ON v.id=l.vessel_id WHERE l.batch_id=$1 ORDER BY l.date, l.id",
            r => new LabRowDto(r.Long("id"), r.LongOrNull("batch_id"), r.Str("date"), r.LongOrNull("vessel_id"), r.Str("stage"), r.Dbl("sugar_gl"), r.Dbl("temp_c"),
            r.Dbl("ph"), r.Dbl("ta_gl"), r.Dbl("rs_gl"), r.Str("note"), r.Str("source"), r.Str("vessel")), batchId);
    }

    public async Task<List<ReportLabRowDto>> ListForReportAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("""
            SELECT l.id, l.date, v.code AS vessel, l.stage, l.sugar_gl, l.temp_c, l.ph, l.ta_gl, l.rs_gl, l.note, l.source FROM lab l
            LEFT JOIN vessels v ON v.id=l.vessel_id WHERE l.batch_id=$1 ORDER BY l.date, l.id
            """, r => new ReportLabRowDto(r.Long("id"), r.Str("date"), r.Str("vessel"), r.Str("stage"), r.Dbl("sugar_gl"), r.Dbl("temp_c"), r.Dbl("ph"), r.Dbl("ta_gl"),
            r.Dbl("rs_gl"), r.Str("note"), r.Str("source")), batchId);
    }

    public async Task<List<InitialSugarDto>> InitialSugarAsync(long? batchId, long? vesselId)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("""
            SELECT l.date, l.sugar_gl, l.source, b.code AS batch_code FROM lab l JOIN batches b ON b.id=l.batch_id
            WHERE l.sugar_gl IS NOT NULL AND ($1 IS NULL OR l.batch_id=$1) AND ($2 IS NULL OR l.vessel_id=$2)
              AND l.date=(SELECT MIN(l2.date) FROM lab l2 WHERE l2.batch_id=l.batch_id AND l2.sugar_gl IS NOT NULL)
            ORDER BY b.code
            """, r => new InitialSugarDto(r.Str("date"), r.Dbl("sugar_gl"), r.Str("source"), r.Str("batch_code")), batchId, vesselId);
    }

    public async Task InsertAsync(NewLabRow l)
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync("INSERT INTO lab(batch_id,date,vessel_id,stage,sugar_gl,temp_c,ph,ta_gl,rs_gl,note,source) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)",
            l.BatchId, l.Date, l.VesselId, l.Stage, l.SugarGl, l.TempC, l.Ph, l.TaGl, l.RsGl, l.Note, l.Source);
    }

    public async Task<List<DatasetLabRow>> ListDatasetRowsAsync()
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("SELECT * FROM lab WHERE source IN ('Dataset A3','Dataset A4.1','Dataset A4.2') ORDER BY id",
            r => new DatasetLabRow(r.Long("batch_id"), r.LongOrNull("vessel_id"), r.Str("date")!, r.Str("source")!, r.Str("stage"), r.Str("note"), r.Dbl("temp_c"), r.Dbl("ph"), r.Dbl("ta_gl")));
    }

    public async Task DeleteForBatchAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync("DELETE FROM lab WHERE batch_id=$1", batchId);
    }
}
