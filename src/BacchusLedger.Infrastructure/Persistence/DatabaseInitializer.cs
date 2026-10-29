using BacchusLedger.Application.Options;
using BacchusLedger.Application.Repositories;
using Microsoft.Extensions.Options;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    ISqliteConnectionFactory connections, IUnitOfWork unitOfWork, IWineryRepository wineries, IReadingRepository readings,
    DatabaseSeeder seeder, IOptions<StorageOptions> storage) : IDatabaseInitializer
{
    const string Schema = """
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
    -- LIVE mode: simulated (or sensor-stub) events only; never read by journals, the loss engine or ROI numbers.
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

    /// Columns added after the first release, added to an existing database file on start.
    static readonly (string Table, string Column, string Type)[] Migrations =
    [
        ("vessels", "cellar_label", "TEXT"), ("vessels", "cellar_label_source", "TEXT"), ("vessels", "depth_cm", "REAL"),
        ("vessels", "neck_d_cm", "REAL"), ("vessels", "max_d_cm", "REAL"), ("vessels", "known_volume_l", "REAL"),
    ];

    public async Task InitializeAsync(bool reset)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(storage.Value.DbPath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        if (reset) await DropAllAsync();
        await unitOfWork.WriteAsync(async () =>
        {
            await using var db = await connections.OpenAsync();
            foreach (var (t, col, type) in Migrations)
            {
                var cols = (await db.QueryAsync($"PRAGMA table_info({t})", r => r.Str("name")!)).ToHashSet();
                if (cols.Count > 0 && !cols.Contains(col)) await db.ExecAsync($"ALTER TABLE {t} ADD COLUMN {col} {type}");
            }
            await db.ExecAsync(Schema);
            return 0;
        });
        if ((await wineries.ListAsync()).Count == 0) await unitOfWork.WriteAsync(async () => { await seeder.SeedAsync(); return 0; });
        // Databases created before readings existed: add the dataset readings once.
        else if (await readings.CountFromDatasetAsync() == 0) await unitOfWork.WriteAsync(async () => { await seeder.SeedReadingsFromLabAsync(); return 0; });
    }

    public async Task ResetAndSeedAsync()
    {
        await DropAllAsync();
        await InitializeAsync(false);
    }

    Task DropAllAsync() => connections.RunExclusiveAsync(db => db.ExecAsync("""
        PRAGMA foreign_keys=OFF;
        DROP TABLE IF EXISTS batch_origins; DROP TABLE IF EXISTS operations; DROP TABLE IF EXISTS lab; DROP TABLE IF EXISTS deliveries;
        DROP TABLE IF EXISTS orders; DROP TABLE IF EXISTS plots; DROP TABLE IF EXISTS farmers; DROP TABLE IF EXISTS sensor_readings;
        DROP TABLE IF EXISTS vessels; DROP TABLE IF EXISTS batches; DROP TABLE IF EXISTS wineries; DROP TABLE IF EXISTS settings;
        DROP TABLE IF EXISTS demo_progress; DROP TABLE IF EXISTS demo_rows; DROP TABLE IF EXISTS readings; DROP TABLE IF EXISTS maintenance; DROP TABLE IF EXISTS live_events; DROP TABLE IF EXISTS seal_chain; DROP TABLE IF EXISTS seal_batch; DROP TABLE IF EXISTS seal_tamper; DROP TABLE IF EXISTS seal_meta;
        """));
}
