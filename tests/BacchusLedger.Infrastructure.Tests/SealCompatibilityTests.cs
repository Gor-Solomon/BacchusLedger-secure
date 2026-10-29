using BacchusLedger.Application.Repositories;
using BacchusLedger.Domain.Seal;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace BacchusLedger.Infrastructure.Tests;

/// The seal hashes every column of a row. Rows read through EF Core must give exactly the same values (and types) as the old
/// hand-written reader did, so chains sealed before EF Core still verify.
public class SealCompatibilityTests
{
    /// The old reader: SELECT * and SqliteDataReader.GetValue (INTEGER → long, REAL → double, TEXT → string, NULL → null).
    static Dictionary<string, object?> OldRow(SqliteConnection c, string table, long id)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {table} WHERE id={id}";
        using var r = cmd.ExecuteReader();
        r.Read();
        var row = new Dictionary<string, object?>();
        for (int i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
        return row;
    }

    [Theory]
    [InlineData("deliveries", "INSERT INTO deliveries(order_id,plot_id,farmer_id,date,kg,sugar_gl,quality_note,accepted,source) VALUES(NULL,NULL,NULL,'2025-09-18',5200,233.0,NULL,1,'DEMO ASSUMPTION')")]
    [InlineData("operations", "INSERT INTO operations(batch_id,date,type,from_vessel_id,to_vessel_id,litres,bottles,bottle_ml,remainder_l,method,dip_cm,person,note,source,created_at) VALUES(NULL,'2026-04-16','bottling',NULL,NULL,9630,12840,750,20,'bottle count',NULL,NULL,'n · ü','Dataset A6.2','2026-10-29T10:00:00.0000000Z')")]
    [InlineData("lab", "INSERT INTO lab(batch_id,date,vessel_id,stage,sugar_gl,temp_c,ph,ta_gl,rs_gl,note,source) VALUES(NULL,'2025-10-15',NULL,'on pomace',NULL,17.6,3.46,6.0,2.3,NULL,'Dataset A4.2')")]
    [InlineData("readings", "INSERT INTO readings(batch_id,vessel_id,taken_at,type,value,unit,instrument,person,note,source) VALUES(NULL,NULL,'2025-09-18T09:00','temperature',21.8,'°C',NULL,NULL,'time not in dataset · day 1','Dataset A3')")]
    public async Task Rows_read_with_EF_Core_hash_exactly_like_rows_read_by_the_old_code(string table, string insert)
    {
        using var t = new TestDatabase();
        using (var scope = t.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(reset: false);

        long id;
        Dictionary<string, object?> old;
        using (var c = t.OpenRaw())
        {
            // the old code wrote rows with plain SQL like this
            TestDatabase.Exec(c, "PRAGMA foreign_keys=OFF; " + insert);
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT last_insert_rowid()";
            id = (long)cmd.ExecuteScalar()!;
            old = OldRow(c, table, id);
        }

        using var s = t.Services.CreateScope();
        var row = (await s.ServiceProvider.GetRequiredService<ISealRepository>().GetRowAsync(table, id))!;

        Assert.Equal(old.Keys.Order(), row.Keys.Order());
        foreach (var (k, v) in old)
        {
            Assert.Equal(v, row[k]);
            Assert.Equal(v?.GetType(), row[k]?.GetType());
        }
        Assert.Equal(HashChain.Start("B").Append(table, old), HashChain.Start("B").Append(table, row));
    }
}
