using BacchusLedger.Application.Repositories;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class SqliteJournalRepository(ISqliteConnectionFactory connections) : IJournalRepository
{
    const string VesselName = "CASE WHEN COALESCE(v.cellar_label,'')='' THEN v.code ELSE v.cellar_label || ' (' || v.code || ')' END";

    public async Task<List<Dictionary<string, object?>>> RowsAsync(int journal, long? batch, string? winery)
    {
        var filter = new RecordFilter(batch, winery);
        await using var db = await connections.OpenAsync();
        switch (journal)
        {
            case 1:
            {
                var (w, a) = filter.ForPlots();
                return await db.QueryRowsAsync($"""
                    SELECT f.name AS farmer, p.code AS plot, p.cadastre_no, p.municipality, p.region, p.area_ha, p.variety, p.licence_no, wi.code AS winery,
                      CASE WHEN p.is_demo=1 THEN 'DEMO ASSUMPTION' ELSE 'Entered' END AS source
                    FROM plots p JOIN farmers f ON f.id=p.farmer_id LEFT JOIN wineries wi ON wi.id=p.winery_id WHERE {w} ORDER BY p.code
                    """, a);
            }
            case 2:
            {
                var (w, a) = filter.ForDeliveries();
                return await db.QueryRowsAsync($"""
                    SELECT d.date, f.name AS farmer, p.code AS plot, d.kg, d.sugar_gl, CASE WHEN d.accepted=1 THEN 'yes' ELSE 'no' END AS accepted,
                      (SELECT group_concat(b.code) FROM batch_origins bo JOIN batches b ON b.id=bo.batch_id WHERE bo.delivery_id=d.id) AS batch, d.source
                    FROM deliveries d JOIN farmers f ON f.id=d.farmer_id JOIN plots p ON p.id=d.plot_id WHERE {w} ORDER BY d.date, d.id
                    """, a);
            }
            case 3:
            {
                var (w, a) = filter.ForBatches("b");
                return await db.QueryRowsAsync($"""
                    SELECT d.date, f.name AS farmer, p.code AS plot, bo.kg, ROUND(bo.kg*100.0/b.grapes_kg, 2) AS share_pct, b.code AS batch,
                      (SELECT {VesselName} FROM operations o JOIN vessels v ON v.id=o.to_vessel_id WHERE o.batch_id=b.id AND o.type='intake' ORDER BY o.date LIMIT 1) AS vessel,
                      (SELECT o.date FROM operations o WHERE o.batch_id=b.id AND o.type='intake' ORDER BY o.date LIMIT 1) AS intake_date, d.source
                    FROM batch_origins bo JOIN batches b ON b.id=bo.batch_id JOIN deliveries d ON d.id=bo.delivery_id
                    JOIN farmers f ON f.id=d.farmer_id JOIN plots p ON p.id=d.plot_id WHERE {w} ORDER BY b.code, d.date, d.id
                    """, a);
            }
            case 4:
            {
                var (w, a) = filter.ForDeliveries();
                return await db.QueryRowsAsync($"""
                    SELECT p.code AS plot, f.name AS farmer, p.variety, p.area_ha, SUM(d.kg) AS kg, ROUND(SUM(d.kg)/p.area_ha, 0) AS kg_per_ha,
                      substr(MIN(d.date),1,4) AS vintage, CASE WHEN p.is_demo=1 THEN 'DEMO ASSUMPTION' ELSE 'Entered' END AS source
                    FROM deliveries d JOIN plots p ON p.id=d.plot_id JOIN farmers f ON f.id=p.farmer_id WHERE {w} GROUP BY p.id ORDER BY p.code
                    """, a);
            }
            case 7:
            {
                var (w, a) = filter.ForBatches("b");
                var rows = await db.QueryRowsAsync($"""
                    SELECT l.date, b.code AS batch, {VesselName} AS vessel, l.stage, l.sugar_gl, l.temp_c, l.ph, l.ta_gl, l.rs_gl, l.note, l.source
                    FROM lab l JOIN batches b ON b.id=l.batch_id LEFT JOIN vessels v ON v.id=l.vessel_id WHERE {w} ORDER BY b.code, l.date, l.id
                    """, a);
                // Readings typed into the app are lab analyses too; dataset readings are already lab rows above.
                rows.AddRange(await db.QueryRowsAsync($"""
                    SELECT substr(r.taken_at,1,10) AS date, b.code AS batch,
                      CASE WHEN v.id IS NULL THEN NULL WHEN COALESCE(v.cellar_label,'')='' THEN v.code ELSE v.cellar_label || ' (' || v.code || ')' END AS vessel,
                      'reading' AS stage, CASE WHEN r.type='temperature' THEN r.value END AS temp_c, CASE WHEN r.type='ph' THEN r.value END AS ph,
                      CASE WHEN r.type='ta' THEN r.value END AS ta_gl,
                      r.type || ' ' || r.value || ' ' || r.unit || ' at ' || substr(r.taken_at,12,5) || COALESCE(' · ' || r.instrument, '') || COALESCE(' · ' || r.person, '') AS note,
                      r.source FROM readings r JOIN batches b ON b.id=r.batch_id LEFT JOIN vessels v ON v.id=r.vessel_id
                    WHERE r.source LIKE 'Entered%' AND {w} ORDER BY b.code, r.taken_at, r.id
                    """, a));
                return rows;
            }
            case 8:
            {
                var (w, a) = filter.ForBatches("b");
                return await db.QueryRowsAsync($"""
                    SELECT b.code AS batch, b.wine_type, b.variety, b.vintage, b.abv, '' AS tasting_date, '' AS members, '' AS appearance, '' AS aroma, '' AS taste, '' AS score, '' AS decision,
                      'Dataset A2' AS source FROM batches b WHERE {w} ORDER BY b.code
                    """, a);
            }
            default: throw new ArgumentOutOfRangeException(nameof(journal), "journals 5 and 6 come from the loss engine");
        }
    }
}
