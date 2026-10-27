using Microsoft.Data.Sqlite;

namespace BacchusLedger;

/// Section 4 seed data. Numbers are exactly as given in BACCHUS_LEDGER_CLI_PROMPT.md.
public static class Seed
{
    public record WineryRow(string Code, string Size, double YearlyT, bool Qvevri, string Level);
    public static readonly WineryRow[] Wineries =
    [
        new("W01", "Large", 2000, true, "L1"),
        new("W02", "Medium", 300, true, "L1"),
        new("W03", "Small to medium", 100, true, "L2"),
        new("W04", "Small", 80, true, "L1"),
        new("W05", "Medium", 500, false, "L2"),
    ];

    public record FarmerPlot(string FarmerCode, string Farmer, string Plot, string Cadastre, double Ha, string Variety, double Kg, string Winery);
    public static readonly FarmerPlot[] W01Farmers =
    [
        new("DF-1", "Demo Farmer 1", "K-101", "DEMO-KV-0001", 0.8, "Saperavi", 5200, "W01"),
        new("DF-2", "Demo Farmer 2", "K-102", "DEMO-KV-0002", 0.6, "Saperavi", 4300, "W01"),
        new("DF-3", "Demo Farmer 3", "K-103", "DEMO-KV-0003", 0.5, "Saperavi", 3800, "W01"),
        new("DF-4", "Demo Farmer 4", "K-104", "DEMO-KV-0004", 0.4, "Saperavi", 2700, "W01"),
    ];
    public static readonly FarmerPlot W03Farmer = new("DF-5", "Demo Farmer 5", "P-301", "DEMO-KV-0005", 0.3, "Rkatsiteli", 1850, "W03");

    // Terroir reference point M1, Kvareli (Dataset A1 + B2)
    public const double Lat = 41.92, Lng = 45.78, SoilPh = 6.7, RainMm = 603.8, SummerT = 24.3;
    public const string SoilTexture = "clay 27.3–30.6%, silt 46.3–48.2%, sand 23.1–24.7%";
    public const string Municipality = "Kvareli", Region = "Kakheti";

    public static readonly string[] PdoMunicipalities = ["Akhmeta", "Telavi", "Gurjaani", "Sighnaghi", "Dedoplistskaro", "Kvareli", "Lagodekhi", "Sagarejo"];

    public const double PriceSaperavi = 1.50, PriceOther = 1.20;

    public record BatchRow(string Code, string Winery, string Date, double Kg, string WineType, string Tech, string Variety, double? Abv);
    public static readonly BatchRow[] Batches =
    [
        new("W01-B1-2025", "W01", "2025-09-18", 16000, "Classic red wine", "classic", "Saperavi", 13.5),
        new("W02-B2-2025", "W02", "2025-09-22", 9800, "Classic white wine", "classic", "Rkatsiteli", null),
        new("W03-B3-2025", "W03", "2025-09-19", 1850, "Qvevri amber wine", "qvevri", "Rkatsiteli", 12.8),
        new("W04-B4-2025", "W04", "2025-09-27", 1500, "Qvevri red wine", "qvevri", "Saperavi", null),
        new("W05-B5-2025", "W05", "2025-09-21", 7000, "Classic white wine", "classic", "Rkatsiteli", null),
    ];

    // Dataset A3: W01 batch 1, F-1, daily from 18.09.2025
    public static readonly double[] A3Sugar = [233.0, 230.0, 218.0, 193.0, 159.0, 123.0, 88.0, 60.0, 38.0, 21.0, 10.0, 4.6, 2.6, 2.4];
    public static readonly double[] A3Temp = [21.8, 22.4, 24.1, 26.0, 27.2, 27.5, 27.0, 26.4, 25.8, 25.1, 24.6, 24.0, 23.5, 23.2];
    public static string A3Date(int i) => new DateTime(2025, 9, 18).AddDays(i).ToString("yyyy-MM-dd");

    // Dataset A4.1: W02 batch 2
    public static readonly (string Date, int Day, double Ph, double Ta)[] A41 =
    [
        ("2025-09-23", 1, 3.18, 7.1), ("2025-09-26", 4, 3.16, 7.3), ("2025-09-29", 7, 3.17, 7.2), ("2025-10-02", 10, 3.20, 7.0),
        ("2025-10-05", 13, 3.22, 6.9), ("2025-10-08", 16, 3.23, 6.8), ("2025-10-11", 19, 3.23, 6.8),
    ];

    // Dataset A4.2: W03 batch 3
    public record LabRow(string Date, string Vessel, string Stage, double Temp, double Ph, double Ta, double Rs);
    public static readonly LabRow[] A42 =
    [
        new("2025-10-15", "Q-3", "on pomace", 17.6, 3.46, 6.0, 2.3),
        new("2025-11-20", "T3-1", "storage", 15.2, 3.47, 5.9, 2.3),
        new("2025-12-15", "T3-2", "ageing", 12.8, 3.47, 5.8, 2.2),
        new("2026-03-26", "T3-2", "ageing", 14.0, 3.48, 5.8, 2.2),
        new("2026-06-09", "T3-2", "before bottling", 16.1, 3.48, 5.7, 2.2),
    ];

    public record OpRow(string Date, string Type, string? From, string? To, double? Litres, int? Bottles = null, double? Ml = null, double? Remainder = null, string? Note = null);

    // Dataset A6.2: W01 batch 1 (paper log)
    public static readonly OpRow[] W01Ops =
    [
        new("2025-09-18", "intake", null, "F-1", null, Note: "16,000 kg grapes"),
        new("2025-10-02", "separation", "F-1", "T1-1", 10100),
        new("2025-10-12", "racking", "T1-1", "T1-2", 9820),
        new("2025-12-15", "stock_check", "T1-2", null, 9820),
        new("2026-03-12", "racking", "T1-2", "T1-3", 9720),
        new("2026-04-14", "filtration", "T1-3", "BT-1", 9670),
        new("2026-04-16", "bottling", "BT-1", null, 9630, 12840, 750, 20),
        new("2026-04-17", "warehouse", null, null, null, 12840, 750, Note: "to finished goods"),
    ];

    // Dataset A6.1: W03 batch 3
    public static readonly OpRow[] W03Ops =
    [
        new("2025-09-19", "intake", null, "Q-3", null, Note: "1,850 kg grapes"),
        new("2025-11-15", "separation", "Q-3", "T3-1", 1230),
        new("2025-12-12", "racking", "T3-1", "T3-2", 1200),
        new("2026-03-26", "stock_check", "T3-2", null, 1200),
        new("2026-06-10", "filtration", "T3-2", "T3-3", 1188),
        new("2026-06-12", "bottling", "T3-3", null, 1182, 1576, 750, 0),
        new("2026-06-13", "warehouse", null, null, null, 1576, 750, Note: "to finished goods"),
    ];

    public static void Run(SqliteConnection c, SqliteTransaction tx)
    {
        foreach (var (k, v) in Db.DefaultSettings)
            Db.Exec(c, tx, "INSERT OR REPLACE INTO settings(key,value) VALUES($1,$2)", k, v);

        int i = 1;
        foreach (var w in Wineries)
            Db.Exec(c, tx, "INSERT INTO wineries(code,name,size,yearly_t,uses_qvevri,readiness_level) VALUES($1,$2,$3,$4,$5,$6)",
                w.Code, $"Winery {i++}", w.Size, w.YearlyT, w.Qvevri ? 1 : 0, w.Level);

        // Vessels (section 4.8) — tank sizes DEMO ASSUMPTION, qvevri shape Dataset A5
        void Tank(string w, string code, string name, double d, double h) =>
            Db.Exec(c, tx, "INSERT INTO vessels(winery_id,code,type,name,calibration_json,capacity_l,source) VALUES((SELECT id FROM wineries WHERE code=$1),$2,'tank',$3,$4,$5,'DEMO ASSUMPTION')",
                w, code, name, Calibration.CylinderJson(d, h), Math.Round(Calibration.CylinderLitres(d, h), 1));
        var qj = Calibration.QvevriJson();
        var qcap = Math.Round(Calibration.Parse(qj).Capacity, 1);
        void Qvevri(string w, string code) =>
            Db.Exec(c, tx, "INSERT INTO vessels(winery_id,code,type,name,calibration_json,capacity_l,source) VALUES((SELECT id FROM wineries WHERE code=$1),$2,'qvevri',$3,$4,$5,'Dataset A5')",
                w, code, $"Qvevri {code}", qj, qcap);

        Tank("W01", "F-1", "Fermentation tank F-1", 230, 290);
        Tank("W01", "F-2", "Fermentation tank F-2", 230, 290);
        for (int t = 1; t <= 5; t++) Tank("W01", $"T1-{t}", $"Tank T1-{t}", 220, 290);
        Tank("W01", "BT-1", "Bottling tank BT-1", 210, 290);
        Qvevri("W03", "Q-3");
        Qvevri("W03", "Q-4");
        for (int t = 1; t <= 3; t++) Tank("W03", $"T3-{t}", $"Tank T3-{t}", 110, 160);

        // Batches (Dataset A2)
        foreach (var b in Batches)
        {
            var status = b.Code is "W01-B1-2025" or "W03-B3-2025" ? "bottled" : "intake recorded";
            InsertBatch(c, tx, b, b.Code, status, "Dataset A2");
        }

        // Farmers/plots/orders/deliveries — DEMO ASSUMPTION (the dataset has no farmers)
        var w01Batch = BatchId(c, tx, "W01-B1-2025");
        foreach (var f in W01Farmers)
        {
            var (fid, pid) = EnsureFarmerPlot(c, tx, f);
            var did = InsertOrderDelivery(c, tx, f, fid, pid, "2025-09-18", 233.0, "DEMO ASSUMPTION");
            Db.Exec(c, tx, "INSERT INTO batch_origins(batch_id,delivery_id,kg) VALUES($1,$2,$3)", w01Batch, did, f.Kg);
        }
        var w03Batch = BatchId(c, tx, "W03-B3-2025");
        {
            var (fid, pid) = EnsureFarmerPlot(c, tx, W03Farmer);
            var did = InsertOrderDelivery(c, tx, W03Farmer, fid, pid, "2025-09-19", null, "DEMO ASSUMPTION");
            Db.Exec(c, tx, "INSERT INTO batch_origins(batch_id,delivery_id,kg) VALUES($1,$2,$3)", w03Batch, did, W03Farmer.Kg);
        }

        // Operations
        foreach (var o in W01Ops) InsertOp(c, tx, w01Batch, "W01", o, "Dataset A6.2");
        foreach (var o in W03Ops) InsertOp(c, tx, w03Batch, "W03", o, "Dataset A6.1");
        foreach (var b in Batches.Where(b => b.Code is not ("W01-B1-2025" or "W03-B3-2025")))
            InsertOp(c, tx, BatchId(c, tx, b.Code), b.Winery, new OpRow(b.Date, "intake", null, null, null, Note: $"{b.Kg:N0} kg grapes (vessel not given in the dataset)"), "Dataset A2");

        // Lab
        for (int d = 0; d < A3Sugar.Length; d++) InsertA3(c, tx, w01Batch, d);
        var w02Batch = BatchId(c, tx, "W02-B2-2025");
        foreach (var r in A41)
        {
            Db.Exec(c, tx, "INSERT INTO lab(batch_id,date,vessel_id,stage,ph,ta_gl,note,source) VALUES($1,$2,NULL,'fermentation',$3,$4,$5,'Dataset A4.1')",
                w02Batch, r.Date, r.Ph, r.Ta, $"fermentation day {r.Day}");
            A41Readings(c, tx, w02Batch, r);
        }
        foreach (var r in A42) InsertA42(c, tx, w03Batch, r);
    }

    // ---------- helpers shared with DemoScript ----------
    public static long BatchId(SqliteConnection c, SqliteTransaction tx, string code) =>
        Db.Query(c, "SELECT id FROM batches WHERE code=$1", code) is { Count: > 0 } r ? (long)r[0]["id"]! : 0;

    public static long VesselId(SqliteConnection c, string winery, string code) =>
        (long)Db.Query(c, "SELECT v.id FROM vessels v JOIN wineries w ON w.id=v.winery_id WHERE w.code=$1 AND v.code=$2", winery, code)[0]["id"]!;

    public static long InsertBatch(SqliteConnection c, SqliteTransaction tx, BatchRow b, string code, string status, string sourceNote) =>
        Db.Insert(c, tx, "INSERT INTO batches(winery_id,code,name,wine_type,technology,variety,vintage,abv,status,source_note,grapes_kg) VALUES((SELECT id FROM wineries WHERE code=$1),$2,$3,$4,$5,$6,2025,$7,$8,$9,$10)",
            b.Winery, code, $"{b.WineType} 2025", b.WineType, b.Tech, b.Variety, b.Abv, status, sourceNote, b.Kg);

    public static (long farmerId, long plotId) EnsureFarmerPlot(SqliteConnection c, SqliteTransaction tx, FarmerPlot f)
    {
        var fr = Db.Query(c, "SELECT id FROM farmers WHERE code=$1", f.FarmerCode);
        long fid = fr.Count > 0 ? (long)fr[0]["id"]! : Db.Insert(c, tx, "INSERT INTO farmers(code,name,phone,is_demo) VALUES($1,$2,NULL,1)", f.FarmerCode, f.Farmer);
        var pr = Db.Query(c, "SELECT id FROM plots WHERE code=$1", f.Plot);
        long pid = pr.Count > 0 ? (long)pr[0]["id"]! : Db.Insert(c, tx,
            """
            INSERT INTO plots(farmer_id,winery_id,code,cadastre_no,village,municipality,region,area_ha,variety,lat,lng,licence_no,soil_texture,soil_ph,rain_mm,summer_temp_c,is_demo)
            VALUES($1,(SELECT id FROM wineries WHERE code=$2),$3,$4,NULL,$5,$6,$7,$8,$9,$10,'DEMO',$11,$12,$13,$14,1)
            """, fid, f.Winery, f.Plot, f.Cadastre, Municipality, Region, f.Ha, f.Variety, Lat, Lng, SoilTexture, SoilPh, RainMm, SummerT);
        return (fid, pid);
    }

    public static long InsertOrder(SqliteConnection c, SqliteTransaction tx, FarmerPlot f, long fid, long pid, string date, string source) =>
        Db.Insert(c, tx, "INSERT INTO orders(winery_id,farmer_id,plot_id,variety,kg_planned,price_gel_per_kg,date,is_demo,source) VALUES((SELECT id FROM wineries WHERE code=$1),$2,$3,$4,$5,$6,$7,1,$8)",
            f.Winery, fid, pid, f.Variety, f.Kg, f.Variety == "Saperavi" ? PriceSaperavi : PriceOther, date, source);

    public static long InsertDelivery(SqliteConnection c, SqliteTransaction tx, FarmerPlot f, long orderId, long fid, long pid, string date, double? sugar, string source) =>
        Db.Insert(c, tx, "INSERT INTO deliveries(order_id,plot_id,farmer_id,date,kg,sugar_gl,quality_note,accepted,source) VALUES($1,$2,$3,$4,$5,$6,NULL,1,$7)",
            orderId, pid, fid, date, f.Kg, sugar, source);

    static long InsertOrderDelivery(SqliteConnection c, SqliteTransaction tx, FarmerPlot f, long fid, long pid, string date, double? sugar, string source)
    {
        var oid = InsertOrder(c, tx, f, fid, pid, date, source);
        return InsertDelivery(c, tx, f, oid, fid, pid, date, sugar, source);
    }

    public static long InsertOp(SqliteConnection c, SqliteTransaction tx, long batchId, string winery, OpRow o, string source, string method = "paper log")
    {
        long? from = o.From == null ? null : VesselId(c, winery, o.From);
        long? to = o.To == null ? null : VesselId(c, winery, o.To);
        return Db.Insert(c, tx,
            "INSERT INTO operations(batch_id,date,type,from_vessel_id,to_vessel_id,litres,bottles,bottle_ml,remainder_l,method,dip_cm,person,note,source,created_at) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,NULL,NULL,$11,$12,$13)",
            batchId, o.Date, o.Type, from, to, o.Litres, o.Bottles, o.Ml, o.Remainder, method, o.Note, source, DateTime.UtcNow.ToString("o"));
    }

    public static void InsertA3(SqliteConnection c, SqliteTransaction tx, long batchId, int d)
    {
        Db.Exec(c, tx, "INSERT INTO lab(batch_id,date,vessel_id,stage,sugar_gl,temp_c,note,source) VALUES($1,$2,$3,'fermentation',$4,$5,$6,'Dataset A3')",
            batchId, A3Date(d), VesselId(c, "W01", "F-1"), A3Sugar[d], A3Temp[d], $"day {d + 1}");
        A3Reading(c, tx, batchId, d);
    }

    public static void InsertA42(SqliteConnection c, SqliteTransaction tx, long batchId, LabRow r)
    {
        Db.Exec(c, tx, "INSERT INTO lab(batch_id,date,vessel_id,stage,temp_c,ph,ta_gl,rs_gl,source) VALUES($1,$2,$3,$4,$5,$6,$7,$8,'Dataset A4.2')",
            batchId, r.Date, VesselId(c, "W03", r.Vessel), r.Stage, r.Temp, r.Ph, r.Ta, r.Rs);
        A42Readings(c, tx, batchId, r);
    }

    // ---------- Update 2: fermentation readings (only what the dataset has) ----------
    // Dataset rows carry dates only: stored at 09:00 and marked "time not in dataset". Instrument is not in the dataset either.
    public const string NoTime = "time not in dataset";

    static void Reading(SqliteConnection c, SqliteTransaction tx, long batchId, long? vesselId, string date, string type, double value, string source, string? note = null) =>
        Db.Exec(c, tx, "INSERT INTO readings(batch_id,vessel_id,taken_at,type,value,unit,instrument,person,note,source) VALUES($1,$2,$3,$4,$5,$6,NULL,NULL,$7,$8)",
            batchId, vesselId, date + "T09:00", type, value, Readings.Units[type], note == null ? NoTime : $"{NoTime} · {note}", source);

    static void A3Reading(SqliteConnection c, SqliteTransaction tx, long batchId, int d) =>
        Reading(c, tx, batchId, VesselId(c, "W01", "F-1"), A3Date(d), "temperature", A3Temp[d], "Dataset A3", $"day {d + 1}");

    static void A41Readings(SqliteConnection c, SqliteTransaction tx, long batchId, (string Date, int Day, double Ph, double Ta) r)
    {
        Reading(c, tx, batchId, null, r.Date, "ph", r.Ph, "Dataset A4.1", $"fermentation day {r.Day}");
        Reading(c, tx, batchId, null, r.Date, "ta", r.Ta, "Dataset A4.1", $"fermentation day {r.Day}");
    }

    static void A42Readings(SqliteConnection c, SqliteTransaction tx, long batchId, LabRow r)
    {
        var v = VesselId(c, "W03", r.Vessel);
        Reading(c, tx, batchId, v, r.Date, "temperature", r.Temp, "Dataset A4.2", r.Stage);
        Reading(c, tx, batchId, v, r.Date, "ph", r.Ph, "Dataset A4.2", r.Stage);
        Reading(c, tx, batchId, v, r.Date, "ta", r.Ta, "Dataset A4.2", r.Stage);
    }

    /// For databases created before Update 2: derive the readings from the dataset lab rows already stored
    /// (seeded batches and any demo replay copies) — the same values, nothing new.
    public static void SeedReadings(SqliteConnection c, SqliteTransaction tx)
    {
        foreach (var l in Db.Query(c, "SELECT * FROM lab WHERE source IN ('Dataset A3','Dataset A4.1','Dataset A4.2') ORDER BY id"))
        {
            long b = (long)l["batch_id"]!; long? v = Db.L(l["vessel_id"]); var date = (string)l["date"]!; var src = (string)l["source"]!;
            var note = src == "Dataset A4.2" ? l["stage"] as string : l["note"] as string;
            if (src != "Dataset A4.1" && Db.D(l["temp_c"]) is { } tc) Reading(c, tx, b, v, date, "temperature", tc, src, note);
            if (src != "Dataset A3" && Db.D(l["ph"]) is { } ph) Reading(c, tx, b, v, date, "ph", ph, src, note);
            if (src != "Dataset A3" && Db.D(l["ta_gl"]) is { } ta) Reading(c, tx, b, v, date, "ta", ta, src, note);
        }
    }
}
