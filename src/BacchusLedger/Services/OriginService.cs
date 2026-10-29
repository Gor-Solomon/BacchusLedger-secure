using System.Text.Json;
using BacchusLedger.Contracts.Common;
using BacchusLedger.Contracts.Origins;
using BacchusLedger.Extensions;
using Microsoft.Data.Sqlite;

namespace BacchusLedger.Services;

public sealed class OriginService : IOriginService
{
    public Task<IReadOnlyList<FarmerDto>> ListFarmersAsync() => Task.FromResult<IReadOnlyList<FarmerDto>>(Db.Query("""
        SELECT f.*, (SELECT COUNT(*) FROM plots p WHERE p.farmer_id=f.id) AS plots, (SELECT SUM(kg) FROM deliveries d WHERE d.farmer_id=f.id) AS kg_delivered
        FROM farmers f ORDER BY f.code
        """).Select(f => new FarmerDto((long)f["id"]!, f["code"] as string, f["name"] as string, f["phone"] as string, Db.L(f["is_demo"]), (long)f["plots"]!, Db.D(f["kg_delivered"]))).ToList());

    public Task<ServiceResult<IdResponse>> AddFarmerAsync(JsonElement body)
    {
        var name = body.Str("name");
        if (string.IsNullOrEmpty(name)) return Task.FromResult(ServiceResult<IdResponse>.BadRequest("Name is required"));
        var phone = body.RawStr("phone");
        var id = Db.Write((c, tx) =>
        {
            var code = "F-" + ((long)Db.Query(c, "SELECT COUNT(*) n FROM farmers")[0]["n"]! + 1).ToString("000");
            return Db.Insert(c, tx, "INSERT INTO farmers(code,name,phone,is_demo) VALUES($1,$2,$3,0)", code, name, phone);
        });
        return Task.FromResult<ServiceResult<IdResponse>>(new IdResponse(id));
    }

    public Task<IReadOnlyList<PlotDto>> ListPlotsAsync() => Task.FromResult<IReadOnlyList<PlotDto>>(Db.Query("""
        SELECT p.*, f.name AS farmer, w.code AS winery, (SELECT SUM(kg) FROM deliveries d WHERE d.plot_id=p.id) AS kg_delivered
        FROM plots p JOIN farmers f ON f.id=p.farmer_id LEFT JOIN wineries w ON w.id=p.winery_id ORDER BY p.code
        """).Select(p => new PlotDto((long)p["id"]!, Db.L(p["farmer_id"]), Db.L(p["winery_id"]), p["code"] as string, p["cadastre_no"] as string, p["village"] as string,
            p["municipality"] as string, p["region"] as string, Db.D(p["area_ha"]), p["variety"] as string, Db.D(p["lat"]), Db.D(p["lng"]), p["licence_no"] as string,
            p["soil_texture"] as string, Db.D(p["soil_ph"]), Db.D(p["rain_mm"]), Db.D(p["summer_temp_c"]), Db.L(p["is_demo"]), p["farmer"] as string, p["winery"] as string,
            Db.D(p["kg_delivered"]), Seed.PdoMunicipalities.Contains(p["municipality"] as string ?? ""))).ToList());

    public Task<ServiceResult<IdResponse>> AddPlotAsync(JsonElement body)
    {
        if (body.Num("farmer_id") is not { } fid || string.IsNullOrEmpty(body.Str("code")))
            return Task.FromResult(ServiceResult<IdResponse>.BadRequest("Farmer and plot code are required"));
        try
        {
            var id = Db.Write((c, tx) => Db.Insert(c, tx, """
                INSERT INTO plots(farmer_id,winery_id,code,cadastre_no,village,municipality,region,area_ha,variety,licence_no,is_demo)
                VALUES($1,(SELECT id FROM wineries WHERE code=$2),$3,$4,$5,$6,'Kakheti',$7,$8,$9,0)
                """, (long)fid, body.Str("winery"), body.Str("code"), body.Str("cadastre_no"), body.Str("village"), body.Str("municipality"), body.Num("area_ha"),
                body.Str("variety"), body.Str("licence_no")));
            return Task.FromResult<ServiceResult<IdResponse>>(new IdResponse(id));
        }
        catch (SqliteException e) { return Task.FromResult(ServiceResult<IdResponse>.BadRequest(e.Message)); }
    }

    public Task<IReadOnlyList<DeliveryDto>> ListDeliveriesAsync() => Task.FromResult<IReadOnlyList<DeliveryDto>>(Db.Query("""
        SELECT d.*, f.name AS farmer, p.code AS plot, w.code AS winery,
          (SELECT group_concat(b.code) FROM batch_origins bo JOIN batches b ON b.id=bo.batch_id WHERE bo.delivery_id=d.id) AS batches
        FROM deliveries d JOIN farmers f ON f.id=d.farmer_id JOIN plots p ON p.id=d.plot_id LEFT JOIN wineries w ON w.id=p.winery_id ORDER BY d.date, d.id
        """).Select(d => new DeliveryDto((long)d["id"]!, Db.L(d["order_id"]), Db.L(d["plot_id"]), Db.L(d["farmer_id"]), d["date"] as string, Db.D(d["kg"]), Db.D(d["sugar_gl"]),
            d["quality_note"] as string, Db.L(d["accepted"]), d["source"] as string, d["farmer"] as string, d["plot"] as string, d["winery"] as string, d["batches"] as string)).ToList());

    public Task<ServiceResult<IdResponse>> AddDeliveryAsync(JsonElement body)
    {
        if (body.Num("plot_id") is not { } pid || body.Num("kg") is not { } kg || kg <= 0)
            return Task.FromResult(ServiceResult<IdResponse>.BadRequest("Plot and kg are required"));
        var id = Db.Write((c, tx) =>
        {
            var fid = Db.Query(c, "SELECT farmer_id FROM plots WHERE id=$1", (long)pid)[0]["farmer_id"];
            var did = Db.Insert(c, tx, "INSERT INTO deliveries(order_id,plot_id,farmer_id,date,kg,sugar_gl,quality_note,accepted,source) VALUES(NULL,$1,$2,$3,$4,$5,$6,$7,'Entered')",
                (long)pid, fid, body.RawStr("date") ?? DateTime.UtcNow.ToString("yyyy-MM-dd"), kg, body.Num("sugar_gl"), body.RawStr("quality_note"), (body.Num("accepted") ?? 1) > 0 ? 1 : 0);
            if (body.Num("batch_id") is { } bid)
                Db.Exec(c, tx, "INSERT INTO batch_origins(batch_id,delivery_id,kg) VALUES($1,$2,$3)", (long)bid, did, kg);
            return did;
        });
        return Task.FromResult<ServiceResult<IdResponse>>(new IdResponse(id));
    }
}
