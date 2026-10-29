using BacchusLedger.Application.Repositories;
using BacchusLedger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BacchusLedger.Infrastructure.Tests;

public class MigrationTests
{
    static readonly string[] Tables =
    [
        "batch_origins", "batches", "deliveries", "demo_progress", "demo_rows", "farmers", "lab", "live_events", "maintenance", "operations", "orders", "outbox_messages", "plots", "processed_messages",
        "readings", "seal_batch", "seal_chain", "seal_meta", "seal_tamper", "sensor_readings", "settings", "vessels", "wineries",
    ];

    static readonly string[] Triggers = ["operations_no_delete", "operations_no_update", "readings_no_delete", "readings_no_update"];

    [Fact]
    public async Task Migration_on_an_empty_database_creates_every_table_and_trigger()
    {
        using var t = new TestDatabase();
        using (var scope = t.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<BacchusLedgerDbContext>().Database.MigrateAsync();

        using var c = t.OpenRaw();
        Assert.Equal(Tables, TestDatabase.Names(c, "table").Where(n => !n.StartsWith("sqlite_") && n != "__EFMigrationsHistory"));
        Assert.Equal(Triggers, TestDatabase.Names(c, "trigger"));
        Assert.Contains("__EFMigrationsHistory", TestDatabase.Names(c, "table"));
    }

    [Fact]
    public async Task Initializer_seeds_an_empty_database()
    {
        using var t = new TestDatabase();
        using var scope = t.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(reset: false);
        var batches = await scope.ServiceProvider.GetRequiredService<IBatchRepository>().ListAsync();
        Assert.Equal(5, batches.Count);
    }

    [Fact]
    public async Task A_database_made_before_EF_Core_is_kept_and_recorded_as_InitialSchema()
    {
        using var t = new TestDatabase();
        using (var c = t.OpenRaw())
        {
            TestDatabase.Exec(c, LegacySchema.Sql);
            TestDatabase.Exec(c, "INSERT INTO wineries(code,name,size,yearly_t,uses_qvevri,readiness_level) VALUES('W99','Old winery','Small',10,1,'L1')");
            TestDatabase.Exec(c, "INSERT INTO readings(batch_id,vessel_id,taken_at,type,value,unit,source) VALUES(NULL,NULL,'2025-01-01T09:00','ph',3.4,'pH','Dataset X')");
        }

        using (var scope = t.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(reset: false);

        using var raw = t.OpenRaw();
        using var cmd = raw.CreateCommand();
        cmd.CommandText = "SELECT (SELECT COUNT(*) FROM __EFMigrationsHistory), (SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId LIMIT 1), " +
            "(SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1), (SELECT name FROM wineries WHERE code='W99'), (SELECT COUNT(*) FROM wineries)";
        using var r = cmd.ExecuteReader();
        r.Read();
        Assert.Equal(2, r.GetInt64(0));
        Assert.EndsWith("_InitialSchema", r.GetString(1)); // recorded, not run
        Assert.EndsWith("_AddOutbox", r.GetString(2));     // run on top of the old tables
        Assert.Equal("Old winery", r.GetString(3)); // the old data is still there
        Assert.Equal(1, r.GetInt64(4)); // and nothing was seeded on top of it
        Assert.Equal(Triggers, TestDatabase.Names(raw, "trigger"));
        Assert.Contains("outbox_messages", TestDatabase.Names(raw, "table"));
    }

    [Fact]
    public async Task Reset_gives_a_fresh_seeded_database_in_the_same_file()
    {
        using var t = new TestDatabase();
        using (var scope = t.Services.CreateScope())
        {
            var init = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
            await init.InitializeAsync(reset: false);
            await scope.ServiceProvider.GetRequiredService<ISettingsRepository>().SaveAsync([new("value_per_litre_usd", "9.99")]);
            await init.ResetAndSeedAsync();
        }
        using (var scope = t.Services.CreateScope())
        {
            var settings = await scope.ServiceProvider.GetRequiredService<ISettingsRepository>().GetAsync();
            Assert.Equal("2.99", settings["value_per_litre_usd"]);
            Assert.Equal(5, (await scope.ServiceProvider.GetRequiredService<IBatchRepository>().ListAsync()).Count);
        }
    }
}
