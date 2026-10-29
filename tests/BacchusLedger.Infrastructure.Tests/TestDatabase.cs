using BacchusLedger.Application;
using BacchusLedger.Application.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace BacchusLedger.Infrastructure.Tests;

/// A temporary SQLite file with the app's real application + infrastructure services on top of it.
public sealed class TestDatabase : IDisposable
{
    readonly string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bacchus-infra-tests-" + Guid.NewGuid().ToString("N"));

    /// configure: extra or replaced services (registered last, so they win).
    public TestDatabase(Action<IServiceCollection>? configure = null)
    {
        Directory.CreateDirectory(dir);
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<StorageOptions>(o => { o.DbPath = Path; o.SigningKeyPath = System.IO.Path.Combine(dir, "key.pem"); });
        services.AddApplication();
        services.AddInfrastructure();
        configure?.Invoke(services);
        Services = services.BuildServiceProvider();
    }

    public string Path => System.IO.Path.Combine(dir, "bacchus.db");
    public ServiceProvider Services { get; }

    /// Raw ADO.NET on the same file (to build an "old" database or to read it the way the old code did).
    public SqliteConnection OpenRaw()
    {
        var c = new SqliteConnection($"Data Source={Path};Pooling=False");
        c.Open();
        return c;
    }

    public static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public static List<string> Names(SqliteConnection c, string type)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT name FROM sqlite_master WHERE type='{type}' ORDER BY name";
        using var r = cmd.ExecuteReader();
        var list = new List<string>();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    public void Dispose()
    {
        Services.Dispose();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
