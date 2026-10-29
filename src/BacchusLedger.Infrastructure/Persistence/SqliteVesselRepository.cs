using BacchusLedger.Application.Contracts.Vessels;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class SqliteVesselRepository(ISqliteConnectionFactory connections) : IVesselRepository
{
    const string Select = "SELECT v.*, w.code AS winery_code FROM vessels v JOIN wineries w ON w.id=v.winery_id";

    public async Task<VesselRecord?> FindAsync(string codeOrLabel, string? winery) => (await Vessels(Select +
        " WHERE (v.code=$1 OR (COALESCE(v.cellar_label,'')<>'' AND lower(trim(v.cellar_label))=lower(trim($1)))) AND ($2 IS NULL OR w.code=$2)" +
        " ORDER BY v.code=$1 DESC, v.id", codeOrLabel, winery)).FirstOrDefault();

    public async Task<VesselRecord?> GetAsync(string winery, string code) => (await Vessels(Select + " WHERE w.code=$1 AND v.code=$2", winery, code)).FirstOrDefault();

    public async Task<long> GetIdAsync(string winery, string code) =>
        (await GetAsync(winery, code))?.Id ?? throw new InvalidOperationException($"vessel {winery}/{code} not found");

    public async Task<bool> ExistsAsync(string winery, string code) => await GetAsync(winery, code) != null;

    public Task<List<VesselRecord>> ListAsync() => Vessels(Select + " ORDER BY w.code, v.id");

    public Task<List<VesselRecord>> ListForCellarAsync() => Vessels(Select + " ORDER BY w.code, v.type DESC, v.id");

    public async Task<List<long>> ListIdsAsync(long batchId, string code)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("SELECT v.id FROM vessels v JOIN batches b ON b.winery_id=v.winery_id WHERE b.id=$1 AND v.code=$2", r => r.Long("id"), batchId, code);
    }

    public async Task<List<VesselBatchDto>> ListBatchesUsingAsync(long vesselId)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("""
            SELECT b.id, b.code, MAX(x.d) AS last FROM batches b JOIN (
              SELECT batch_id, date AS d FROM operations WHERE from_vessel_id=$1 OR to_vessel_id=$1
              UNION ALL SELECT batch_id, taken_at FROM readings WHERE vessel_id=$1
              UNION ALL SELECT batch_id, date FROM lab WHERE vessel_id=$1) x ON x.batch_id=b.id
            GROUP BY b.id ORDER BY last DESC, (b.source_note LIKE '%copy%'), b.id
            """, r => new VesselBatchDto(r.Long("id"), r.Str("code"), r.Str("last")), vesselId);
    }

    public async Task<BatchWineType?> LastRealBatchAsync(long vesselId)
    {
        await using var db = await connections.OpenAsync();
        return await db.FirstOrDefaultAsync("""
            SELECT b.code, b.wine_type FROM batches b JOIN (
              SELECT batch_id, date AS d FROM operations WHERE from_vessel_id=$1 OR to_vessel_id=$1
              UNION ALL SELECT batch_id, taken_at FROM readings WHERE vessel_id=$1
              UNION ALL SELECT batch_id, date FROM lab WHERE vessel_id=$1) x ON x.batch_id=b.id
            WHERE COALESCE(b.source_note,'') NOT LIKE '%copy%' GROUP BY b.id ORDER BY MAX(x.d) DESC LIMIT 1
            """, r => new BatchWineType(r.Str("code"), r.Str("wine_type")), vesselId);
    }

    public async Task<string?> FirstRealWineTypeAsync(string winery)
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync("""
            SELECT b.wine_type FROM batches b JOIN wineries w ON w.id=b.winery_id WHERE w.code=$1 AND COALESCE(b.source_note,'') NOT LIKE '%copy%' ORDER BY b.code LIMIT 1
            """, r => r.Str("wine_type"), winery)).FirstOrDefault();
    }

    public async Task<bool> LabelUsedAsync(long wineryId, long exceptVesselId, string label)
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync("SELECT 1 x FROM vessels WHERE winery_id=$1 AND id<>$2 AND lower(trim(cellar_label))=lower($3)", r => 1, wineryId, exceptVesselId, label)).Count > 0;
    }

    public async Task<long> InsertAsync(NewVessel v)
    {
        await using var db = await connections.OpenAsync();
        return await db.InsertAsync("""
            INSERT INTO vessels(winery_id,code,type,name,calibration_json,capacity_l,source,cellar_label,cellar_label_source,depth_cm,neck_d_cm,max_d_cm,known_volume_l)
            VALUES((SELECT id FROM wineries WHERE code=$1),$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)
            """, v.WineryCode, v.Code, v.Type, v.Name, v.CalibrationJson, v.CapacityL, v.Source, v.CellarLabel, v.CellarLabelSource, v.DepthCm, v.NeckDCm, v.MaxDCm, v.KnownVolumeL);
    }

    public async Task SetLabelAsync(long id, string? label, string? labelSource)
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync("UPDATE vessels SET cellar_label=$1, cellar_label_source=$2 WHERE id=$3", label, labelSource, id);
    }

    public async Task SetMeasurementsAsync(long id, VesselMeasurements m)
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync("UPDATE vessels SET depth_cm=$1, neck_d_cm=$2, max_d_cm=$3, known_volume_l=$4, calibration_json=$5, capacity_l=$6, source=$7 WHERE id=$8",
            m.DepthCm, m.NeckDCm, m.MaxDCm, m.KnownVolumeL, m.CalibrationJson, m.CapacityL, m.Source, id);
    }

    public async Task DeleteUnusedLiveDemoAsync()
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync("""
            DELETE FROM vessels WHERE source='Entered (live demo)'
              AND id NOT IN (SELECT from_vessel_id FROM operations WHERE from_vessel_id IS NOT NULL UNION SELECT to_vessel_id FROM operations WHERE to_vessel_id IS NOT NULL
                             UNION SELECT vessel_id FROM readings WHERE vessel_id IS NOT NULL UNION SELECT vessel_id FROM lab WHERE vessel_id IS NOT NULL)
            """);
    }

    public async Task ClearLiveDemoLabelsAsync()
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync("UPDATE vessels SET cellar_label=NULL, cellar_label_source=NULL WHERE cellar_label_source='Entered (live demo)'");
    }

    async Task<List<VesselRecord>> Vessels(string sql, params object?[] args)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync(sql, r => new VesselRecord(r.Long("id"), r.Long("winery_id"), r.Str("winery_code")!, r.Str("code")!, r.Str("type")!, r.Str("name"),
            r.Str("calibration_json"), r.Dbl("capacity_l"), r.Str("source"), r.Str("cellar_label"), r.Str("cellar_label_source"), r.Dbl("depth_cm"), r.Dbl("neck_d_cm"),
            r.Dbl("max_d_cm"), r.Dbl("known_volume_l")), args);
    }
}
