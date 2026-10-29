using BacchusLedger.Application.Contracts.Batches;
using BacchusLedger.Application.Contracts.Origins;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Domain.Ledger;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class SqliteOriginRepository(ISqliteConnectionFactory connections) : IOriginRepository
{
    public async Task<List<FarmerDto>> ListFarmersAsync()
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("""
            SELECT f.*, (SELECT COUNT(*) FROM plots p WHERE p.farmer_id=f.id) AS plots, (SELECT SUM(kg) FROM deliveries d WHERE d.farmer_id=f.id) AS kg_delivered
            FROM farmers f ORDER BY f.code
            """, r => new FarmerDto(r.Long("id"), r.Str("code"), r.Str("name"), r.Str("phone"), r.LongOrNull("is_demo"), r.Long("plots"), r.Dbl("kg_delivered")));
    }

    public async Task<long> CountFarmersAsync()
    {
        await using var db = await connections.OpenAsync();
        return await db.CountAsync("SELECT COUNT(*) n FROM farmers");
    }

    public Task<long?> FindFarmerIdAsync(string code) => FindId("SELECT id FROM farmers WHERE code=$1", code);

    public async Task<long> InsertFarmerAsync(string code, string name, string? phone, bool isDemo)
    {
        await using var db = await connections.OpenAsync();
        return await db.InsertAsync("INSERT INTO farmers(code,name,phone,is_demo) VALUES($1,$2,$3,$4)", code, name, phone, isDemo ? 1 : 0);
    }

    public async Task<List<PlotDto>> ListPlotsAsync()
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("""
            SELECT p.*, f.name AS farmer, w.code AS winery, (SELECT SUM(kg) FROM deliveries d WHERE d.plot_id=p.id) AS kg_delivered
            FROM plots p JOIN farmers f ON f.id=p.farmer_id LEFT JOIN wineries w ON w.id=p.winery_id ORDER BY p.code
            """, r => new PlotDto(r.Long("id"), r.LongOrNull("farmer_id"), r.LongOrNull("winery_id"), r.Str("code"), r.Str("cadastre_no"), r.Str("village"),
            r.Str("municipality"), r.Str("region"), r.Dbl("area_ha"), r.Str("variety"), r.Dbl("lat"), r.Dbl("lng"), r.Str("licence_no"), r.Str("soil_texture"),
            r.Dbl("soil_ph"), r.Dbl("rain_mm"), r.Dbl("summer_temp_c"), r.LongOrNull("is_demo"), r.Str("farmer"), r.Str("winery"), r.Dbl("kg_delivered"), false));
    }

    public Task<long?> FindPlotIdAsync(string code) => FindId("SELECT id FROM plots WHERE code=$1", code);

    public async Task<long> InsertPlotAsync(NewPlot p)
    {
        await using var db = await connections.OpenAsync();
        return await db.InsertAsync("""
            INSERT INTO plots(farmer_id,winery_id,code,cadastre_no,village,municipality,region,area_ha,variety,lat,lng,licence_no,soil_texture,soil_ph,rain_mm,summer_temp_c,is_demo)
            VALUES($1,(SELECT id FROM wineries WHERE code=$2),$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17)
            """, p.FarmerId, p.WineryCode, p.Code, p.CadastreNo, p.Village, p.Municipality, p.Region, p.AreaHa, p.Variety, p.Lat, p.Lng, p.LicenceNo,
            p.SoilTexture, p.SoilPh, p.RainMm, p.SummerTempC, p.IsDemo);
    }

    public async Task<long?> GetPlotFarmerIdAsync(long plotId)
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync("SELECT farmer_id FROM plots WHERE id=$1", r => r.LongOrNull("farmer_id"), plotId))[0];
    }

    public async Task<long> InsertOrderAsync(NewOrder o)
    {
        await using var db = await connections.OpenAsync();
        return await db.InsertAsync(
            "INSERT INTO orders(winery_id,farmer_id,plot_id,variety,kg_planned,price_gel_per_kg,date,is_demo,source) VALUES((SELECT id FROM wineries WHERE code=$1),$2,$3,$4,$5,$6,$7,1,$8)",
            o.WineryCode, o.FarmerId, o.PlotId, o.Variety, o.KgPlanned, o.PriceGelPerKg, o.Date, o.Source);
    }

    public Task DeleteOrderAsync(long id) => Exec("DELETE FROM orders WHERE id=$1", id);

    public async Task<List<DeliveryDto>> ListDeliveriesAsync()
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("""
            SELECT d.*, f.name AS farmer, p.code AS plot, w.code AS winery,
              (SELECT group_concat(b.code) FROM batch_origins bo JOIN batches b ON b.id=bo.batch_id WHERE bo.delivery_id=d.id) AS batches
            FROM deliveries d JOIN farmers f ON f.id=d.farmer_id JOIN plots p ON p.id=d.plot_id LEFT JOIN wineries w ON w.id=p.winery_id ORDER BY d.date, d.id
            """, r => new DeliveryDto(r.Long("id"), r.LongOrNull("order_id"), r.LongOrNull("plot_id"), r.LongOrNull("farmer_id"), r.Str("date"), r.Dbl("kg"),
            r.Dbl("sugar_gl"), r.Str("quality_note"), r.LongOrNull("accepted"), r.Str("source"), r.Str("farmer"), r.Str("plot"), r.Str("winery"), r.Str("batches")));
    }

    public async Task<long> InsertDeliveryAsync(NewDelivery d)
    {
        await using var db = await connections.OpenAsync();
        return await db.InsertAsync("INSERT INTO deliveries(order_id,plot_id,farmer_id,date,kg,sugar_gl,quality_note,accepted,source) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)",
            d.OrderId, d.PlotId, d.FarmerId, d.Date, d.Kg, d.SugarGl, d.QualityNote, d.Accepted, d.Source);
    }

    public async Task<double?> GetDeliveryKgAsync(long id)
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync("SELECT kg FROM deliveries WHERE id=$1", r => r.Dbl("kg"), id))[0];
    }

    public Task DeleteDeliveryAsync(long id) => Exec("DELETE FROM deliveries WHERE id=$1", id);

    public Task InsertBatchOriginAsync(long batchId, long deliveryId, double? kg) =>
        Exec("INSERT INTO batch_origins(batch_id,delivery_id,kg) VALUES($1,$2,$3)", batchId, deliveryId, kg);

    public Task DeleteBatchOriginsAsync(long batchId) => Exec("DELETE FROM batch_origins WHERE batch_id=$1", batchId);

    public async Task<List<BatchOriginDto>> ListBatchOriginsAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("""
            SELECT bo.kg, ROUND(bo.kg*100.0/b.grapes_kg, 3) AS share_pct, d.id AS delivery_id, d.date, d.sugar_gl, d.source, f.name AS farmer, p.code AS plot, p.area_ha, p.variety,
              p.municipality, p.region, p.cadastre_no, p.lat, p.lng, p.soil_ph, p.soil_texture, p.rain_mm, p.summer_temp_c
            FROM batch_origins bo JOIN batches b ON b.id=bo.batch_id JOIN deliveries d ON d.id=bo.delivery_id JOIN farmers f ON f.id=d.farmer_id JOIN plots p ON p.id=d.plot_id
            WHERE bo.batch_id=$1 ORDER BY d.id
            """, r => new BatchOriginDto(r.Dbl("kg"), r.Dbl("share_pct"), r.Long("delivery_id"), r.Str("date"), r.Dbl("sugar_gl"), r.Str("source"), r.Str("farmer"),
            r.Str("plot"), r.Dbl("area_ha"), r.Str("variety"), r.Str("municipality"), r.Str("region"), r.Str("cadastre_no"), r.Dbl("lat"), r.Dbl("lng"), r.Dbl("soil_ph"),
            r.Str("soil_texture"), r.Dbl("rain_mm"), r.Dbl("summer_temp_c")), batchId);
    }

    public async Task<List<OriginCheckRow>> ListOriginChecksAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("""
            SELECT bo.kg, p.code AS plot, p.municipality FROM batch_origins bo
            JOIN deliveries d ON d.id=bo.delivery_id JOIN plots p ON p.id=d.plot_id WHERE bo.batch_id=$1
            """, r => new OriginCheckRow(r.Dbl("kg"), r.Str("plot"), r.Str("municipality")), batchId);
    }

    public async Task<List<HarvestDelivery>> ListLedgerOriginsAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("""
            SELECT d.id, d.date, bo.kg, d.source, p.code AS plot, p.municipality, f.name AS farmer FROM batch_origins bo
            JOIN deliveries d ON d.id=bo.delivery_id JOIN plots p ON p.id=d.plot_id LEFT JOIN farmers f ON f.id=d.farmer_id
            WHERE bo.batch_id=$1 ORDER BY d.date, d.id
            """, r => new HarvestDelivery(r.Long("id"), r.Str("date")!, r.Dbl("kg"), r.Str("source"), r.Str("plot"), r.Str("municipality"), r.Str("farmer")), batchId);
    }

    public async Task<List<FarmerOrigin>> ListFarmerOriginsAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("""
            SELECT f.name AS farmer, p.code AS plot, p.municipality, bo.kg, d.source FROM batch_origins bo
            JOIN deliveries d ON d.id=bo.delivery_id JOIN farmers f ON f.id=d.farmer_id JOIN plots p ON p.id=d.plot_id WHERE bo.batch_id=$1 ORDER BY d.id
            """, r => new FarmerOrigin(r.Str("farmer"), r.Str("plot"), r.Str("municipality"), r.Dbl("kg"), r.Str("source")), batchId);
    }

    public async Task<List<OriginLink>> ListOriginLinksAsync(string batchCode)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("SELECT bo.delivery_id, bo.kg FROM batch_origins bo JOIN batches b ON b.id=bo.batch_id WHERE b.code=$1",
            r => new OriginLink(r.Long("delivery_id"), r.Dbl("kg")), batchCode);
    }

    async Task<long?> FindId(string sql, string code)
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync(sql, r => r.Long("id"), code)).Cast<long?>().FirstOrDefault();
    }

    async Task Exec(string sql, params object?[] args)
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync(sql, args);
    }
}
