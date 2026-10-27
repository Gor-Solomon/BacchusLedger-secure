using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace BacchusLedger;

/// Section 7: the 8 Kakheti PDO journals (Kakheti PDO spec §12), generated from the records.
public static class Journals
{
    public record Col(string Key, string Label, string? Source = null);

    public sealed class Journal
    {
        public int N { get; set; }
        public string Title { get; set; } = "";
        public string Note { get; set; } = "";
        public List<Col> Columns { get; set; } = [];
        public List<Dictionary<string, object?>> Rows { get; set; } = [];
    }

    public static readonly string[] Titles =
    [
        "", "Vineyard accounting", "Grape receiving", "Grape processing", "Vintage",
        "Storage and movement", "Bottling", "Laboratory analysis", "Tasting commission",
    ];

    static (string sql, object?[] args) BatchFilter(string alias, long? batch, string? winery)
    {
        var parts = new List<string>(); var args = new List<object?>();
        if (batch != null) { args.Add(batch); parts.Add($"{alias}.id=${args.Count}"); }
        if (!string.IsNullOrEmpty(winery)) { args.Add(winery); parts.Add($"{alias}.winery_id=(SELECT id FROM wineries WHERE code=${args.Count})"); }
        return (parts.Count == 0 ? "1=1" : string.Join(" AND ", parts), args.ToArray());
    }

    static List<long> BatchIds(SqliteConnection c, long? batch, string? winery)
    {
        var (w, a) = BatchFilter("b", batch, winery);
        return Db.Query(c, $"SELECT id FROM batches b WHERE {w} ORDER BY b.code", a).Select(r => (long)r["id"]!).ToList();
    }

    public static Journal Build(SqliteConnection c, int n, long? batch, string? winery)
    {
        var j = new Journal { N = n, Title = Titles[n] };
        switch (n)
        {
            case 1:
            {
                j.Columns = [new("farmer", "Farmer"), new("plot", "Plot"), new("cadastre_no", "Cadastre no."), new("municipality", "Municipality"), new("region", "Region"),
                    new("area_ha", "Area ha"), new("variety", "Variety"), new("licence_no", "Licence no."), new("winery", "Winery")];
                var (w, a) = PlotFilter(batch, winery);
                j.Rows = Db.Query(c, $"""
                    SELECT f.name AS farmer, p.code AS plot, p.cadastre_no, p.municipality, p.region, p.area_ha, p.variety, p.licence_no, wi.code AS winery,
                      CASE WHEN p.is_demo=1 THEN 'DEMO ASSUMPTION' ELSE 'Entered' END AS source
                    FROM plots p JOIN farmers f ON f.id=p.farmer_id LEFT JOIN wineries wi ON wi.id=p.winery_id WHERE {w} ORDER BY p.code
                    """, a);
                j.Note = "Farmers and plots are DEMO ASSUMPTIONS — the dataset has no farmers.";
                break;
            }
            case 2:
            {
                j.Columns = [new("date", "Date"), new("farmer", "Farmer"), new("plot", "Plot"), new("kg", "kg"), new("sugar_gl", "Sugar g/L"), new("accepted", "Accepted"), new("batch", "Batch")];
                var (w, a) = DeliveryFilter(batch, winery);
                j.Rows = Db.Query(c, $"""
                    SELECT d.date, f.name AS farmer, p.code AS plot, d.kg, d.sugar_gl, CASE WHEN d.accepted=1 THEN 'yes' ELSE 'no' END AS accepted,
                      (SELECT group_concat(b.code) FROM batch_origins bo JOIN batches b ON b.id=bo.batch_id WHERE bo.delivery_id=d.id) AS batch, d.source
                    FROM deliveries d JOIN farmers f ON f.id=d.farmer_id JOIN plots p ON p.id=d.plot_id WHERE {w} ORDER BY d.date, d.id
                    """, a);
                j.Note = "kg per delivery is a DEMO split of the Dataset A2 batch total. Sugar 233 g/L = Dataset A3 day 1 (must in F-1).";
                break;
            }
            case 3:
            {
                j.Columns = [new("date", "Delivery date"), new("farmer", "Farmer"), new("plot", "Plot"), new("kg", "kg"), new("share_pct", "Share %", "Calculated"),
                    new("batch", "Batch"), new("vessel", "Into vessel"), new("intake_date", "Intake date")];
                var (w, a) = BatchFilter("b", batch, winery);
                j.Rows = Db.Query(c, $"""
                    SELECT d.date, f.name AS farmer, p.code AS plot, bo.kg, ROUND(bo.kg*100.0/b.grapes_kg, 2) AS share_pct, b.code AS batch,
                      (SELECT CASE WHEN COALESCE(v.cellar_label,'')='' THEN v.code ELSE v.cellar_label || ' (' || v.code || ')' END FROM operations o JOIN vessels v ON v.id=o.to_vessel_id WHERE o.batch_id=b.id AND o.type='intake' ORDER BY o.date LIMIT 1) AS vessel,
                      (SELECT o.date FROM operations o WHERE o.batch_id=b.id AND o.type='intake' ORDER BY o.date LIMIT 1) AS intake_date, d.source
                    FROM batch_origins bo JOIN batches b ON b.id=bo.batch_id JOIN deliveries d ON d.id=bo.delivery_id
                    JOIN farmers f ON f.id=d.farmer_id JOIN plots p ON p.id=d.plot_id WHERE {w} ORDER BY b.code, d.date, d.id
                    """, a);
                j.Note = "Links every delivery to its batch and intake vessel. Share % is Calculated.";
                break;
            }
            case 4:
            {
                j.Columns = [new("plot", "Plot"), new("farmer", "Farmer"), new("variety", "Variety"), new("area_ha", "Area ha"), new("kg", "Harvest kg"),
                    new("kg_per_ha", "kg per ha", "Calculated"), new("vintage", "Vintage")];
                var (w, a) = DeliveryFilter(batch, winery);
                j.Rows = Db.Query(c, $"""
                    SELECT p.code AS plot, f.name AS farmer, p.variety, p.area_ha, SUM(d.kg) AS kg, ROUND(SUM(d.kg)/p.area_ha, 0) AS kg_per_ha,
                      substr(MIN(d.date),1,4) AS vintage, CASE WHEN p.is_demo=1 THEN 'DEMO ASSUMPTION' ELSE 'Entered' END AS source
                    FROM deliveries d JOIN plots p ON p.id=d.plot_id JOIN farmers f ON f.id=p.farmer_id WHERE {w} GROUP BY p.id ORDER BY p.code
                    """, a);
                j.Note = "kg per ha = harvest kg ÷ plot area (Calculated).";
                break;
            }
            case 5:
            {
                j.Columns = [new("date", "Date"), new("batch", "Batch"), new("op", "Operation"), new("from", "From"), new("to", "To"), new("before_l", "Before L", "Calculated"),
                    new("litres", "Litres"), new("remainder_l", "Remainder L"), new("loss_l", "Loss L", "Calculated"), new("loss_pct", "Loss %", "Calculated"),
                    new("status", "Status"), new("method", "Method"), new("note", "Note")];
                var s = Db.Settings();
                foreach (var id in BatchIds(c, batch, winery))
                {
                    var r = LossEngine.Analyze(c, id, s, compare: false);
                    foreach (var st in r.Stages.Where(x => x.Type is not ("bottling" or "warehouse")))
                        j.Rows.Add(new()
                        {
                            ["date"] = st.Date, ["batch"] = r.BatchCode, ["op"] = st.StageKey, ["from"] = Fmt.Vessel(st.From, st.FromLabel), ["to"] = Fmt.Vessel(st.To, st.ToLabel), ["before_l"] = st.BeforeL,
                            ["litres"] = st.AfterL, ["remainder_l"] = st.RemainderL, ["loss_l"] = st.LossL, ["loss_pct"] = st.LossPct, ["status"] = st.Status,
                            ["method"] = st.Method, ["note"] = st.Note, ["source"] = st.Source,
                        });
                }
                j.Note = "Every cellar movement with from/to, litres and loss (mass balance, Calculated). Bottling runs are in journal 6.";
                break;
            }
            case 6:
            {
                j.Columns = [new("date", "Date"), new("batch", "Batch"), new("op", "Operation"), new("vessel", "From vessel"), new("bottles", "Bottles"), new("bottle_ml", "Bottle mL"),
                    new("litres", "Litres bottled", "Calculated"), new("remainder_l", "Remainder L"), new("loss_l", "Loss L", "Calculated"), new("check", "Bottle check")];
                var s = Db.Settings();
                foreach (var id in BatchIds(c, batch, winery))
                {
                    var r = LossEngine.Analyze(c, id, s, compare: false);
                    foreach (var st in r.Stages.Where(x => x.Type is "bottling" or "warehouse"))
                    {
                        bool isB = st.Type == "bottling";
                        double? lit = isB ? st.AfterL : null;
                        j.Rows.Add(new()
                        {
                            ["date"] = st.Date, ["batch"] = r.BatchCode, ["op"] = st.Type, ["vessel"] = Fmt.Vessel(st.From, st.FromLabel), ["bottles"] = st.Bottles, ["bottle_ml"] = st.BottleMl,
                            ["litres"] = lit, ["remainder_l"] = st.RemainderL, ["loss_l"] = st.LossL,
                            ["check"] = isB ? $"{st.Bottles:N0} × {(st.BottleMl ?? 750) / 1000:0.00#} = {lit:N0} L ✓" : (st.Note ?? ""), ["source"] = st.Source,
                        });
                    }
                }
                j.Note = "Bottling runs and transfer to finished goods.";
                break;
            }
            case 7:
            {
                j.Columns = [new("date", "Date"), new("batch", "Batch"), new("vessel", "Vessel"), new("stage", "Stage"), new("sugar_gl", "Sugar g/L"), new("temp_c", "Temp °C"),
                    new("ph", "pH"), new("ta_gl", "TA g/L"), new("rs_gl", "RS g/L"), new("note", "Note")];
                var (w, a) = BatchFilter("b", batch, winery);
                j.Rows = Db.Query(c, $"""
                    SELECT l.date, b.code AS batch, CASE WHEN COALESCE(v.cellar_label,'')='' THEN v.code ELSE v.cellar_label || ' (' || v.code || ')' END AS vessel, l.stage, l.sugar_gl, l.temp_c, l.ph, l.ta_gl, l.rs_gl, l.note, l.source
                    FROM lab l JOIN batches b ON b.id=l.batch_id LEFT JOIN vessels v ON v.id=l.vessel_id WHERE {w} ORDER BY b.code, l.date, l.id
                    """, a);
                // Readings typed into the app (Update 2) are lab analyses too; dataset readings are already lab rows above.
                j.Rows.AddRange(Db.Query(c, $"""
                    SELECT substr(r.taken_at,1,10) AS date, b.code AS batch,
                      CASE WHEN v.id IS NULL THEN NULL WHEN COALESCE(v.cellar_label,'')='' THEN v.code ELSE v.cellar_label || ' (' || v.code || ')' END AS vessel,
                      'reading' AS stage, CASE WHEN r.type='temperature' THEN r.value END AS temp_c, CASE WHEN r.type='ph' THEN r.value END AS ph,
                      CASE WHEN r.type='ta' THEN r.value END AS ta_gl,
                      r.type || ' ' || r.value || ' ' || r.unit || ' at ' || substr(r.taken_at,12,5) || COALESCE(' · ' || r.instrument, '') || COALESCE(' · ' || r.person, '') AS note,
                      r.source FROM readings r JOIN batches b ON b.id=r.batch_id LEFT JOIN vessels v ON v.id=r.vessel_id
                    WHERE r.source LIKE 'Entered%' AND {w} ORDER BY b.code, r.taken_at, r.id
                    """, a));
                j.Note = "All laboratory rows (Dataset A3, A4.1, A4.2) plus readings entered in the app (Initial Brix, hydrometer, pH, temperature, TA).";
                break;
            }
            case 8:
            {
                j.Columns = [new("batch", "Batch"), new("wine_type", "Wine type"), new("variety", "Variety"), new("vintage", "Vintage"), new("abv", "ABV %"),
                    new("tasting_date", "Tasting date"), new("members", "Commission members"), new("appearance", "Appearance"), new("aroma", "Aroma"),
                    new("taste", "Taste"), new("score", "Score"), new("decision", "Decision")];
                var (w, a) = BatchFilter("b", batch, winery);
                j.Rows = Db.Query(c, $"""
                    SELECT b.code AS batch, b.wine_type, b.variety, b.vintage, b.abv, '' AS tasting_date, '' AS members, '' AS appearance, '' AS aroma, '' AS taste, '' AS score, '' AS decision,
                      'Dataset A2' AS source FROM batches b WHERE {w} ORDER BY b.code
                    """, a);
                j.Note = "Empty template — there is no tasting data in the dataset. Variety and ABV are DEMO ASSUMPTIONS.";
                break;
            }
            default: throw new ArgumentOutOfRangeException(nameof(n));
        }
        return j;
    }

    static (string, object?[]) PlotFilter(long? batch, string? winery)
    {
        var parts = new List<string>(); var args = new List<object?>();
        if (batch != null) { args.Add(batch); parts.Add($"p.id IN (SELECT d.plot_id FROM batch_origins bo JOIN deliveries d ON d.id=bo.delivery_id WHERE bo.batch_id=${args.Count})"); }
        if (!string.IsNullOrEmpty(winery)) { args.Add(winery); parts.Add($"p.winery_id=(SELECT id FROM wineries WHERE code=${args.Count})"); }
        return (parts.Count == 0 ? "1=1" : string.Join(" AND ", parts), args.ToArray());
    }

    static (string, object?[]) DeliveryFilter(long? batch, string? winery)
    {
        var parts = new List<string>(); var args = new List<object?>();
        if (batch != null) { args.Add(batch); parts.Add($"d.id IN (SELECT delivery_id FROM batch_origins WHERE batch_id=${args.Count})"); }
        if (!string.IsNullOrEmpty(winery)) { args.Add(winery); parts.Add($"p.winery_id=(SELECT id FROM wineries WHERE code=${args.Count})"); }
        return (parts.Count == 0 ? "1=1" : string.Join(" AND ", parts), args.ToArray());
    }

    /// UTF-8 with BOM so Excel opens Georgian text.
    public static byte[] Csv(Journal j)
    {
        var sb = new StringBuilder();
        static string Esc(object? v)
        {
            var s = v switch { null => "", double d => d.ToString(CultureInfo.InvariantCulture), IFormattable f => f.ToString(null, CultureInfo.InvariantCulture), _ => v.ToString() ?? "" };
            return s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
        sb.AppendLine($"# Journal {j.N}: {Esc(j.Title)} (Kakheti PDO spec §12)");
        sb.AppendLine(string.Join(",", j.Columns.Select(c => Esc(c.Label)).Append("Source")));
        foreach (var r in j.Rows)
            sb.AppendLine(string.Join(",", j.Columns.Select(c => Esc(r.GetValueOrDefault(c.Key))).Append(Esc(r.GetValueOrDefault("source")))));
        sb.AppendLine($"# {Esc(j.Note)}");
        var pre = Encoding.UTF8.GetPreamble();
        return [.. pre, .. Encoding.UTF8.GetBytes(sb.ToString())];
    }
}
