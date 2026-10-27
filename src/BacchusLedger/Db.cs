using System.Globalization;
using Microsoft.Data.Sqlite;

namespace BacchusLedger;

public static class Db
{
    public static string Path = Environment.GetEnvironmentVariable("DB_PATH") ?? "/data/bacchus.db";
    static readonly object WriteLock = new();

    public static SqliteConnection Open()
    {
        var c = new SqliteConnection($"Data Source={Path}");
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();
        return c;
    }

    /// Serialise all writes (SQLite allows one writer anyway).
    public static T Write<T>(Func<SqliteConnection, SqliteTransaction, T> work)
    {
        lock (WriteLock)
        {
            using var c = Open();
            using var tx = c.BeginTransaction();
            var r = work(c, tx);
            tx.Commit();
            return r;
        }
    }

    public const string Schema = """
    CREATE TABLE IF NOT EXISTS wineries(id INTEGER PRIMARY KEY, code TEXT UNIQUE, name TEXT, size TEXT, yearly_t REAL, uses_qvevri INTEGER, readiness_level TEXT);
    CREATE TABLE IF NOT EXISTS farmers(id INTEGER PRIMARY KEY, code TEXT UNIQUE, name TEXT, phone TEXT, is_demo INTEGER);
    CREATE TABLE IF NOT EXISTS plots(id INTEGER PRIMARY KEY, farmer_id INTEGER REFERENCES farmers(id), winery_id INTEGER REFERENCES wineries(id), code TEXT UNIQUE,
        cadastre_no TEXT, village TEXT, municipality TEXT, region TEXT, area_ha REAL, variety TEXT, lat REAL, lng REAL, licence_no TEXT,
        soil_texture TEXT, soil_ph REAL, rain_mm REAL, summer_temp_c REAL, is_demo INTEGER);
    CREATE TABLE IF NOT EXISTS orders(id INTEGER PRIMARY KEY, winery_id INTEGER REFERENCES wineries(id), farmer_id INTEGER REFERENCES farmers(id), plot_id INTEGER REFERENCES plots(id),
        variety TEXT, kg_planned REAL, price_gel_per_kg REAL, date TEXT, is_demo INTEGER, source TEXT);
    CREATE TABLE IF NOT EXISTS deliveries(id INTEGER PRIMARY KEY, order_id INTEGER REFERENCES orders(id), plot_id INTEGER REFERENCES plots(id), farmer_id INTEGER REFERENCES farmers(id),
        date TEXT, kg REAL, sugar_gl REAL, quality_note TEXT, accepted INTEGER, source TEXT);
    CREATE TABLE IF NOT EXISTS vessels(id INTEGER PRIMARY KEY, winery_id INTEGER REFERENCES wineries(id), code TEXT, type TEXT CHECK(type IN ('tank','qvevri')), name TEXT,
        calibration_json TEXT, capacity_l REAL, source TEXT,
        cellar_label TEXT, cellar_label_source TEXT, depth_cm REAL, neck_d_cm REAL, max_d_cm REAL, known_volume_l REAL, UNIQUE(winery_id, code));
    CREATE TABLE IF NOT EXISTS batches(id INTEGER PRIMARY KEY, winery_id INTEGER REFERENCES wineries(id), code TEXT UNIQUE, name TEXT, wine_type TEXT,
        technology TEXT CHECK(technology IN ('classic','qvevri')), variety TEXT, vintage INTEGER, abv REAL, status TEXT, source_note TEXT, grapes_kg REAL);
    CREATE TABLE IF NOT EXISTS batch_origins(batch_id INTEGER REFERENCES batches(id), delivery_id INTEGER REFERENCES deliveries(id), kg REAL);
    CREATE TABLE IF NOT EXISTS operations(id INTEGER PRIMARY KEY, batch_id INTEGER REFERENCES batches(id), date TEXT,
        type TEXT CHECK(type IN ('intake','separation','racking','stock_check','filtration','transfer','bottling','warehouse')),
        from_vessel_id INTEGER REFERENCES vessels(id), to_vessel_id INTEGER REFERENCES vessels(id), litres REAL, bottles INTEGER, bottle_ml REAL, remainder_l REAL,
        method TEXT CHECK(method IS NULL OR method IN ('paper log','dip','bottle count','scale')), dip_cm REAL, person TEXT, note TEXT, source TEXT, created_at TEXT);
    CREATE TABLE IF NOT EXISTS lab(id INTEGER PRIMARY KEY, batch_id INTEGER REFERENCES batches(id), date TEXT, vessel_id INTEGER REFERENCES vessels(id), stage TEXT,
        sugar_gl REAL, temp_c REAL, ph REAL, ta_gl REAL, rs_gl REAL, note TEXT, source TEXT);
    CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY, value TEXT);
    CREATE TABLE IF NOT EXISTS sensor_readings(id INTEGER PRIMARY KEY, vessel_id INTEGER REFERENCES vessels(id), dip_cm REAL, litres REAL, created_at TEXT);
    CREATE TABLE IF NOT EXISTS demo_progress(script TEXT, step INTEGER, PRIMARY KEY(script, step));
    CREATE TABLE IF NOT EXISTS demo_rows(script TEXT, tbl TEXT, row_id INTEGER);
    CREATE TABLE IF NOT EXISTS readings(id INTEGER PRIMARY KEY, batch_id INTEGER REFERENCES batches(id), vessel_id INTEGER REFERENCES vessels(id),
        taken_at TEXT, type TEXT CHECK(type IN ('initial_brix','ph','temperature','hydrometer_density','hydrometer_abv','ta')),
        value REAL, unit TEXT, instrument TEXT, person TEXT, note TEXT, source TEXT);
    -- Update 3: LIVE mode. Simulated (or sensor-stub) events only; never read by journals, the loss engine or ROI numbers.
    CREATE TABLE IF NOT EXISTS live_events(id INTEGER PRIMARY KEY, created_at TEXT, sim_time TEXT, vessel_id INTEGER, vessel_code TEXT, vessel_name TEXT,
        kind TEXT CHECK(kind IN ('reading','notification')), metric TEXT, value REAL, unit TEXT, level TEXT, rule TEXT, message TEXT, value_usd REAL,
        scenario TEXT, source TEXT NOT NULL CHECK(source IN ('SIMULATED','SENSOR')), acknowledged INTEGER DEFAULT 0);
    -- A row here (inside a reset transaction only) is the one exception to the append-only rule.
    CREATE TABLE IF NOT EXISTS maintenance(reason TEXT);
    -- Append-only: operations and readings may never be changed or removed (demo reset is the only exception).
    CREATE TRIGGER IF NOT EXISTS operations_no_update BEFORE UPDATE ON operations BEGIN SELECT RAISE(ABORT, 'operations are append-only'); END;
    CREATE TRIGGER IF NOT EXISTS operations_no_delete BEFORE DELETE ON operations WHEN NOT EXISTS (SELECT 1 FROM maintenance)
        BEGIN SELECT RAISE(ABORT, 'operations are append-only'); END;
    CREATE TRIGGER IF NOT EXISTS readings_no_update BEFORE UPDATE ON readings BEGIN SELECT RAISE(ABORT, 'readings are append-only'); END;
    CREATE TRIGGER IF NOT EXISTS readings_no_delete BEFORE DELETE ON readings WHEN NOT EXISTS (SELECT 1 FROM maintenance)
        BEGIN SELECT RAISE(ABORT, 'readings are append-only'); END;
    """;

    /// Columns added in Update 2 — added to an existing database file on start.
    static readonly (string Table, string Column, string Type)[] Migrations =
    [
        ("vessels", "cellar_label", "TEXT"), ("vessels", "cellar_label_source", "TEXT"), ("vessels", "depth_cm", "REAL"),
        ("vessels", "neck_d_cm", "REAL"), ("vessels", "max_d_cm", "REAL"), ("vessels", "known_volume_l", "REAL"),
    ];

    static void Migrate(SqliteConnection c, SqliteTransaction tx)
    {
        foreach (var (t, col, type) in Migrations)
        {
            var cols = Query(c, $"PRAGMA table_info({t})").Select(r => (string)r["name"]!).ToHashSet();
            if (cols.Count > 0 && !cols.Contains(col)) Exec(c, tx, $"ALTER TABLE {t} ADD COLUMN {col} {type}");
        }
    }

    /// Runs `work` with the append-only delete guard lifted (demo reset only), inside the caller's transaction.
    public static void AllowDelete(SqliteConnection c, SqliteTransaction tx, Action work)
    {
        Exec(c, tx, "INSERT INTO maintenance(reason) VALUES('demo reset')");
        work();
        Exec(c, tx, "DELETE FROM maintenance");
    }

    /// Runs work in a transaction that is always rolled back (selftest probes).
    public static T Probe<T>(Func<SqliteConnection, SqliteTransaction, T> work)
    {
        lock (WriteLock)
        {
            using var c = Open();
            using var tx = c.BeginTransaction();
            try { return work(c, tx); } finally { tx.Rollback(); }
        }
    }

    public static void Init(bool reset)
    {
        var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        if (reset) ResetAll();
        Write((c, tx) => { Migrate(c, tx); Exec(c, tx, Schema); return 0; });
        if (Scalar<long>("SELECT COUNT(*) FROM wineries") == 0) Write((c, tx) => { Seed.Run(c, tx); return 0; });
        // Databases created before Update 2: add the dataset readings once.
        else if (Scalar<long>("SELECT COUNT(*) FROM readings WHERE source LIKE 'Dataset%'") == 0) Write((c, tx) => { Seed.SeedReadings(c, tx); return 0; });
    }

    /// Reset demo data: the only place where rows are removed.
    public static void ResetAll()
    {
        lock (WriteLock)
        {
            using (var c = Open())
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = """
                    PRAGMA foreign_keys=OFF;
                    DROP TABLE IF EXISTS batch_origins; DROP TABLE IF EXISTS operations; DROP TABLE IF EXISTS lab; DROP TABLE IF EXISTS deliveries;
                    DROP TABLE IF EXISTS orders; DROP TABLE IF EXISTS plots; DROP TABLE IF EXISTS farmers; DROP TABLE IF EXISTS sensor_readings;
                    DROP TABLE IF EXISTS vessels; DROP TABLE IF EXISTS batches; DROP TABLE IF EXISTS wineries; DROP TABLE IF EXISTS settings;
                    DROP TABLE IF EXISTS demo_progress; DROP TABLE IF EXISTS demo_rows; DROP TABLE IF EXISTS readings; DROP TABLE IF EXISTS maintenance; DROP TABLE IF EXISTS live_events; DROP TABLE IF EXISTS seal_chain; DROP TABLE IF EXISTS seal_batch; DROP TABLE IF EXISTS seal_tamper; DROP TABLE IF EXISTS seal_meta;
                    """;
                cmd.ExecuteNonQuery();
            }
        }
    }

    public static void Reseed()
    {
        ResetAll();
        Init(false);
    }

    static void Bind(SqliteCommand cmd, object?[] args)
    {
        for (int i = 0; i < args.Length; i++)
            cmd.Parameters.AddWithValue("$" + (i + 1), args[i] ?? DBNull.Value);
    }

    public static int Exec(SqliteConnection c, SqliteTransaction? tx, string sql, params object?[] args)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        Bind(cmd, args);
        return cmd.ExecuteNonQuery();
    }

    public static long Insert(SqliteConnection c, SqliteTransaction? tx, string sql, params object?[] args)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql + "; SELECT last_insert_rowid();";
        Bind(cmd, args);
        return (long)cmd.ExecuteScalar()!;
    }

    public static List<Dictionary<string, object?>> Query(SqliteConnection c, string sql, params object?[] args)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        Bind(cmd, args);
        using var r = cmd.ExecuteReader();
        var list = new List<Dictionary<string, object?>>();
        while (r.Read())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            list.Add(row);
        }
        return list;
    }

    public static List<Dictionary<string, object?>> Query(string sql, params object?[] args)
    {
        using var c = Open();
        return Query(c, sql, args);
    }

    public static T Scalar<T>(string sql, params object?[] args)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        Bind(cmd, args);
        var v = cmd.ExecuteScalar();
        if (v == null || v is DBNull) return default!;
        return (T)Convert.ChangeType(v, typeof(T), CultureInfo.InvariantCulture);
    }

    // ---------- settings ----------
    public static readonly Dictionary<string, string> DefaultSettings = new()
    {
        ["value_per_litre_usd"] = "2.99",
        ["litres_per_tonne"] = "631",
        ["norm_racking_first"] = "3.0",
        ["norm_racking"] = "1.5",
        ["norm_filtration"] = "0.6",
        ["norm_bottling"] = "0.3",
        ["norm_stock_check"] = "0.3",
        ["norm_transfer"] = "1.0",
        ["language"] = "en",
        // Update 3: LIVE rule thresholds — DEMO ASSUMPTIONS (temperature limits moved to config/ranges.json)
        ["live_level_drop_l"] = "20",
        ["live_level_window_h"] = "1",
        ["live_stuck_delta_sg"] = "0.001",
        ["live_stuck_hours"] = "48",
        ["live_stuck_min_sg"] = "1.010",
    };

    public static Dictionary<string, string> Settings()
    {
        var d = new Dictionary<string, string>(DefaultSettings);
        foreach (var r in Query("SELECT key, value FROM settings")) d[(string)r["key"]!] = (string)r["value"]!;
        return d;
    }

    public static double SettingD(Dictionary<string, string> s, string key) =>
        double.Parse(s.TryGetValue(key, out var v) ? v : DefaultSettings[key], CultureInfo.InvariantCulture);

    public static double? D(object? o) => o == null ? null : Convert.ToDouble(o, CultureInfo.InvariantCulture);
    public static long? L(object? o) => o == null ? null : Convert.ToInt64(o, CultureInfo.InvariantCulture);
}
