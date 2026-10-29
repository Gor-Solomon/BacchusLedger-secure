using BacchusLedger.Contracts.Batches;
using BacchusLedger.Services.Mapping;
using Microsoft.Data.Sqlite;

namespace BacchusLedger.Services;

public sealed class BatchService : IBatchService
{
    public Task<IReadOnlyList<BatchSummaryDto>> ListAsync()
    {
        using var c = Db.Open();
        var s = Db.Settings();
        IReadOnlyList<BatchSummaryDto> list = Db.Query(c, "SELECT b.*, w.code AS winery_code FROM batches b JOIN wineries w ON w.id=b.winery_id ORDER BY b.code")
            .Select(b => Summary(c, b, s)).ToList();
        return Task.FromResult(list);
    }

    public Task<ServiceResult<BatchDetailResponse>> GetAsync(long id)
    {
        using var c = Db.Open();
        var b = Db.BatchRow(c, id);
        if (b == null) return Task.FromResult(ServiceResult<BatchDetailResponse>.NotFound("batch not found"));
        var s = Db.Settings();
        var r = LossEngine.Analyze(c, id, s);
        var origins = Db.Query(c, """
            SELECT bo.kg, ROUND(bo.kg*100.0/b.grapes_kg, 3) AS share_pct, d.id AS delivery_id, d.date, d.sugar_gl, d.source, f.name AS farmer, p.code AS plot, p.area_ha, p.variety,
              p.municipality, p.region, p.cadastre_no, p.lat, p.lng, p.soil_ph, p.soil_texture, p.rain_mm, p.summer_temp_c
            FROM batch_origins bo JOIN batches b ON b.id=bo.batch_id JOIN deliveries d ON d.id=bo.delivery_id JOIN farmers f ON f.id=d.farmer_id JOIN plots p ON p.id=d.plot_id
            WHERE bo.batch_id=$1 ORDER BY d.id
            """, id).Select(o => new BatchOriginDto(Db.D(o["kg"]), Db.D(o["share_pct"]), (long)o["delivery_id"]!, o["date"] as string, Db.D(o["sugar_gl"]), o["source"] as string,
                o["farmer"] as string, o["plot"] as string, Db.D(o["area_ha"]), o["variety"] as string, o["municipality"] as string, o["region"] as string, o["cadastre_no"] as string,
                Db.D(o["lat"]), Db.D(o["lng"]), Db.D(o["soil_ph"]), o["soil_texture"] as string, Db.D(o["rain_mm"]), Db.D(o["summer_temp_c"]))).ToList();
        var lab = Db.Query(c, "SELECT l.*, v.code AS vessel FROM lab l LEFT JOIN vessels v ON v.id=l.vessel_id WHERE l.batch_id=$1 ORDER BY l.date, l.id", id)
            .Select(l => new LabRowDto((long)l["id"]!, Db.L(l["batch_id"]), l["date"] as string, Db.L(l["vessel_id"]), l["stage"] as string, Db.D(l["sugar_gl"]), Db.D(l["temp_c"]),
                Db.D(l["ph"]), Db.D(l["ta_gl"]), Db.D(l["rs_gl"]), l["note"] as string, l["source"] as string, l["vessel"] as string)).ToList();
        var totals = new BatchTotalsDto(r.FirstLitres, r.BottledL, r.Bottles, r.RemainderL, r.TotalLossL, r.TotalDifferenceL, r.LitresNow, r.LossValueUsd, r.ExcessL, r.VesselLitres);
        return Task.FromResult<ServiceResult<BatchDetailResponse>>(
            new BatchDetailResponse(Summary(c, b, s), r.Stages.ToDtos(), r.Checks.ToDtos(), origins, lab, totals, s));
    }

    static BatchSummaryDto Summary(SqliteConnection c, Dictionary<string, object?> b, Dictionary<string, string> s)
    {
        var r = LossEngine.Analyze(c, (long)b["id"]!, s, compare: false);
        var intake = r.Stages.FirstOrDefault(x => x.Type == "intake")?.Date ?? Db.Query(c, "SELECT MIN(date) d FROM operations WHERE batch_id=$1", b["id"])[0]["d"] as string;
        return new BatchSummaryDto((long)b["id"]!, b["code"] as string, b["winery_code"] as string, b["name"] as string, b["wine_type"] as string, b["technology"] as string,
            b["variety"] as string, Db.L(b["vintage"]), Db.D(b["abv"]), b["status"] as string, Db.D(b["grapes_kg"]), b["source_note"] as string, Db.IsCopy(b),
            intake, r.FirstLitres, r.LitresNow, r.BottledL, r.Bottles, r.TotalLossL, r.LossValueUsd,
            r.Stages.Count(x => x.Status == "above"), r.Stages.Count(x => x.Status == "negative"));
    }
}
