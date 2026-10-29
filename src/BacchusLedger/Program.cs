using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BacchusLedger;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
var startup = new Startup();
startup.ConfigureServices(builder.Services);
var app = builder.Build();
startup.Configure(app);

// ---------------- settings / reset ----------------
app.MapGet("/api/settings", () => Db.Settings());
app.MapPost("/api/settings", (Dictionary<string, JsonElement> body, LiveService live) =>
{
    foreach (var (k, v) in body)
    {
        if (!Db.DefaultSettings.ContainsKey(k)) return Results.BadRequest(new { error = $"unknown setting {k}" });
        var val = v.ValueKind == JsonValueKind.Number ? v.GetDouble().ToString(CultureInfo.InvariantCulture) : v.GetString() ?? "";
        if (k != "language" && !double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return Results.BadRequest(new { error = $"{k} must be a number" });
    }
    Db.Write((c, tx) =>
    {
        foreach (var (k, v) in body)
            Db.Exec(c, tx, "INSERT OR REPLACE INTO settings(key,value) VALUES($1,$2)", k, v.ValueKind == JsonValueKind.Number ? v.GetDouble().ToString(CultureInfo.InvariantCulture) : v.GetString());
        return 0;
    });
    var saved = Db.Settings();
    live.Sim.UseSettings(saved);
    return Results.Ok(saved);
});
app.MapPost("/api/reset", () => { Db.Reseed(); return Results.Ok(new { reset = true }); });

app.MapGet("/api/selftest", (HttpContext ctx) => Results.Ok(SelfTest.Run($"http://127.0.0.1:{ctx.Connection.LocalPort}", app.Environment.WebRootPath)));

app.Run();
