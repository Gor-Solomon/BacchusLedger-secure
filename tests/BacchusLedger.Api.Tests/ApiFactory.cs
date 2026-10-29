using BacchusLedger.Application.Providers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BacchusLedger.Api.Tests;

/// The real app on a fresh temporary database, a temporary DEMO key and no AI keys (same as scripts/check.ps1).
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "bacchus-tests-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(dir);
        builder.UseSetting("DB_PATH", Path.Combine(dir, "bacchus.db"));
        builder.UseSetting("RESET_ON_START", "true");
        builder.UseSetting("SIGNING_KEY_PATH", Path.Combine(dir, "demo-key.pem"));
        builder.UseSetting("OPENAI_API_KEY", "");
        builder.UseSetting("GEMINI_API_KEY", "");
        // The selftest and the assistant tools call our own API: route those calls through the in-memory test server.
        builder.ConfigureTestServices(s => s.AddSingleton<ILocalApiClient>(_ => new TestServerApiClient(() => Server.CreateClient())));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
