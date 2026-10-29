using BacchusLedger.Application.Providers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BacchusLedger.Api.Tests;

/// The real app on a fresh temporary database, a temporary DEMO key and no AI keys (same as scripts/check.ps1).
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    readonly string dir;
    readonly bool reset, ownsDir;
    readonly IReadOnlyDictionary<string, string> settings;
    readonly Action<IServiceCollection>? services;

    public ApiFactory() : this(null) { }

    /// dir: an existing database folder to reuse (not reset, not deleted); settings and services: test overrides.
    internal ApiFactory(string? dir, IReadOnlyDictionary<string, string>? settings = null, Action<IServiceCollection>? services = null)
    {
        ownsDir = reset = dir == null;
        this.dir = dir ?? Path.Combine(Path.GetTempPath(), "bacchus-tests-" + Guid.NewGuid().ToString("N"));
        this.settings = settings ?? new Dictionary<string, string>();
        this.services = services;
    }

    public string Dir => dir;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(dir);
        builder.UseSetting("DB_PATH", Path.Combine(dir, "bacchus.db"));
        builder.UseSetting("RESET_ON_START", reset ? "true" : "false");
        builder.UseSetting("SIGNING_KEY_PATH", Path.Combine(dir, "demo-key.pem"));
        builder.UseSetting("OPENAI_API_KEY", "");
        builder.UseSetting("GEMINI_API_KEY", "");
        // The selftest and the assistant tools call our own API: route those calls through the in-memory test server.
        foreach (var (key, value) in settings) builder.UseSetting(key, value);
        builder.ConfigureTestServices(s =>
        {
            s.AddSingleton<ILocalApiClient>(_ => new TestServerApiClient(() => Server.CreateClient()));
            services?.Invoke(s);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!ownsDir) return;
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
