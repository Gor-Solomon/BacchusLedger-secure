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

// ---------------- helpers ----------------
/// Find a vessel by internal code or by its cellar label (what is written on the tape in the cellar).
static Dictionary<string, object?>? VesselRow(SqliteConnection c, string code, string? winery) =>
    Db.Query(c, """
        SELECT v.*, w.code AS winery_code FROM vessels v JOIN wineries w ON w.id=v.winery_id
        WHERE (v.code=$1 OR (COALESCE(v.cellar_label,'')<>'' AND lower(trim(v.cellar_label))=lower(trim($1)))) AND ($2 IS NULL OR w.code=$2)
        ORDER BY v.code=$1 DESC, v.id
        """, code, winery).FirstOrDefault();

// "Try: W03 filtration with dip 35.0 cm" — a fresh copy of W03 batch 3 up to the stock check
app.MapPost("/api/try/w03", () => Db.Write((c, tx) =>
{
    var src = Seed.Batches.First(x => x.Code == "W03-B3-2025");
    int k = (int)(Db.Query(c, "SELECT COUNT(*) n FROM batches WHERE code LIKE 'W03-B3-2025-TRY-%'")[0]["n"] as long? ?? 0) + 1;
    var code = $"W03-B3-2025-TRY-{k}";
    var id = Seed.InsertBatch(c, tx, src, code, "in progress (practice copy)", "Dataset A6.1 — practice copy of W03-B3-2025");
    foreach (var o in Db.Query(c, "SELECT bo.delivery_id, bo.kg FROM batch_origins bo JOIN batches b ON b.id=bo.batch_id WHERE b.code='W03-B3-2025'"))
        Db.Exec(c, tx, "INSERT INTO batch_origins(batch_id,delivery_id,kg) VALUES($1,$2,$3)", id, o["delivery_id"], o["kg"]);
    foreach (var o in Seed.W03Ops.Take(4)) Seed.InsertOp(c, tx, id, "W03", o, "Dataset A6.1");
    return Results.Ok(new { batch_id = id, code });
}));

// Third demo player: a clean live-demo batch on F-2 (or a new empty qvevri). Nothing is invented — the presenter types every value.
app.MapPost("/api/demo/readings/start", (string? vessel) => Db.Write((c, tx) =>
{
    const string code = "W01-LIVE-DEMO";
    var old = Db.Query(c, "SELECT id FROM batches WHERE code=$1", code).FirstOrDefault()?["id"];
    Db.AllowDelete(c, tx, () =>
    {
        if (old != null) Db.Exec(c, tx, "DELETE FROM readings WHERE batch_id=$1", old);
    });
    if (old != null) Db.Exec(c, tx, "DELETE FROM batches WHERE id=$1", old);
    Db.Exec(c, tx, """
        DELETE FROM vessels WHERE source='Entered (live demo)'
          AND id NOT IN (SELECT from_vessel_id FROM operations WHERE from_vessel_id IS NOT NULL UNION SELECT to_vessel_id FROM operations WHERE to_vessel_id IS NOT NULL
                         UNION SELECT vessel_id FROM readings WHERE vessel_id IS NOT NULL UNION SELECT vessel_id FROM lab WHERE vessel_id IS NOT NULL)
        """);
    Db.Exec(c, tx, "UPDATE vessels SET cellar_label=NULL, cellar_label_source=NULL WHERE cellar_label_source='Entered (live demo)'");
    var vcode = "F-2";
    if (vessel == "new")
    {
        vcode = "Q-LIVE";
        if (Db.Query(c, "SELECT 1 FROM vessels v JOIN wineries w ON w.id=v.winery_id WHERE w.code='W01' AND v.code=$1", vcode).Count == 0)
            Db.Exec(c, tx, "INSERT INTO vessels(winery_id,code,type,name,calibration_json,capacity_l,source) VALUES((SELECT id FROM wineries WHERE code='W01'),$1,'qvevri','New qvevri (live demo)',NULL,NULL,'Entered (live demo)')", vcode);
    }
    var id = Db.Insert(c, tx, """
        INSERT INTO batches(winery_id,code,name,wine_type,technology,variety,vintage,abv,status,source_note,grapes_kg)
        VALUES((SELECT id FROM wineries WHERE code='W01'),$1,'Live demo batch','Live demo (values entered by the presenter)',$2,NULL,NULL,NULL,'fermenting (live demo)',
          'Entered (live demo) — readings typed during the presentation; live demo copy',NULL)
        """, code, vessel == "new" ? "qvevri" : "classic");
    return Results.Ok(new { batch_id = id, batch_code = code, vessel = vcode, winery = "W01" });
}));

// Optional stub: sensor reading -> calibrated litres
// Feeds the same live pipeline as the simulator (events tagged SENSOR) — a real ESP32 / WINEGRID can replace the simulator later.
app.MapPost("/api/sensors/reading", (JsonElement body, LiveHub hub) =>
{
    var code = body.GetProperty("vessel").GetString()!;
    var dip = body.GetProperty("dip_cm").GetDouble();
    LiveEvent? ev = null;
    var res = Db.Write((c, tx) =>
    {
        var v = VesselRow(c, code, null);
        if (v == null) return Results.NotFound(new { error = "vessel not found" });
        var m = Calibration.TryParse(v["calibration_json"] as string);
        if (m == null) return Results.BadRequest(new { error = "Volume unknown — enter litres manually" });
        var l = Math.Round(m.LitresAtDip(dip), 1);
        Db.Exec(c, tx, "INSERT INTO sensor_readings(vessel_id,dip_cm,litres,created_at) VALUES($1,$2,$3,$4)", v["id"], dip, l, DateTime.UtcNow.ToString("o"));
        ev = LiveSim.SensorEvent((long)v["id"]!, (string)v["code"]!, v["cellar_label"] as string, l);
        ev.Id = Db.Insert(c, tx, "INSERT INTO live_events(created_at,sim_time,vessel_id,vessel_code,vessel_name,kind,metric,value,unit,source) VALUES($1,$2,$3,$4,$5,'reading','level',$6,'L','SENSOR')",
            ev.CreatedAt, ev.SimTime, ev.VesselId, ev.VesselCode, ev.VesselName, l);
        return Results.Ok(new { vessel = code, dip_cm = dip, litres = l, source = "Calculated" });
    });
    if (ev != null) hub.Publish("sensor", ev);
    return res;
});

// ---------------- LIVE mode (Update 3) — SIMULATED ----------------
app.MapGet("/api/live/state", (LiveService live) => Results.Ok(live.Sim.State()));
app.MapPost("/api/live/on", (LiveService live) => { live.TurnOn(); live.Tick(); return Results.Ok(live.Sim.State()); });
app.MapPost("/api/live/off", (LiveService live) => { live.TurnOff(); return Results.Ok(live.Sim.State()); });
app.MapPost("/api/live/scenario/{name}", (string name, LiveService live) =>
{
    if (!LiveSim.Scenarios.Contains(name)) return Results.NotFound(new { error = "unknown scenario" });
    live.SetScenario(name);
    return Results.Ok(live.Sim.State());
});
app.MapGet("/api/live/events", (string? kind, string? vessel, int? limit) => Results.Ok(Db.Query("""
    SELECT * FROM live_events WHERE ($1 IS NULL OR kind=$1) AND ($2 IS NULL OR vessel_code=$2) ORDER BY id DESC LIMIT $3
    """, kind, vessel, Math.Clamp(limit ?? 50, 1, 500))));
app.MapPost("/api/live/events/{id:long}/ack", (long id, LiveHub hub) =>
{
    Db.Write((c, tx) => Db.Exec(c, tx, "UPDATE live_events SET acknowledged=1 WHERE id=$1", id));
    hub.Publish("ack", new { id });
    return Results.Ok(new { id });
});
app.MapPost("/api/live/ack-all", (LiveHub hub) =>
{
    Db.Write((c, tx) => Db.Exec(c, tx, "UPDATE live_events SET acknowledged=1 WHERE kind='notification'"));
    hub.Publish("ack", new { all = true });
    return Results.Ok(new { ok = true });
});
// Simulated data is not a record: clearing it is allowed (it never reaches journals or ROI).
app.MapPost("/api/live/clear", (LiveService live, LiveHub hub) =>
{
    live.TurnOff();
    Db.Write((c, tx) => Db.Exec(c, tx, "DELETE FROM live_events"));
    hub.Publish("cleared", new { ok = true });
    return Results.Ok(new { ok = true });
});
app.MapGet("/api/live/stream", async (HttpContext ctx, LiveHub hub, LiveService live, bool? once) =>
{
    ctx.Response.Headers.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers["X-Accel-Buffering"] = "no";
    var (reader, dispose) = hub.Subscribe();
    try
    {
        await ctx.Response.WriteAsync($"retry: 3000\nevent: state\ndata: {JsonSerializer.Serialize(live.Sim.State(), LiveHub.Json)}\n\n", ctx.RequestAborted);
        await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
        if (once == true) return;
        var ct = ctx.RequestAborted;
        while (!ct.IsCancellationRequested)
        {
            var wait = reader.WaitToReadAsync(ct).AsTask();
            if (await Task.WhenAny(wait, Task.Delay(15000, ct)) != wait) { await ctx.Response.WriteAsync(": keep-alive\n\n", ct); await ctx.Response.Body.FlushAsync(ct); continue; }
            if (!await wait) break;
            while (reader.TryRead(out var msg)) await ctx.Response.WriteAsync(msg, ct);
            await ctx.Response.Body.FlushAsync(ct);
        }
    }
    catch (OperationCanceledException) { }
    finally { dispose(); }
});

// ---------------- demo player ----------------
static string Script(string? s) => s == "w03" ? "w03" : "w01";
app.MapGet("/api/demo/steps", (string? script) => Results.Ok(DemoScript.Steps(Script(script))));
app.MapPost("/api/demo/step/{n:int}", (int n, string? script) =>
{
    try { return Results.Ok(DemoScript.Step(Script(script), n)); }
    catch (ArgumentOutOfRangeException) { return Results.NotFound(new { error = "no such step" }); }
});
app.MapPost("/api/demo/reset", (string? script) =>
{
    DemoScript.Reset(Script(script));
    return Results.Ok(DemoScript.Steps(Script(script)));
});

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

// ---------------- Marani Assistant (Update 4) — read-only ----------------
static string Lang(string? l) => l == "ka" ? "ka" : "en";
app.MapGet("/api/assistant/status", (string? lang) => Results.Ok(Assistant.Status(AssistantConfig.FromEnv(), Lang(lang))));
app.MapGet("/api/assistant/quick/{n:int}", (int n, string? lang) =>
    n is < 1 or > 5 ? Results.NotFound(new { error = "no such question" }) : Results.Ok(Assistant.Quick(n, Lang(lang), Db.Settings())));
app.MapPost("/api/assistant/ask", (HttpContext ctx, JsonElement body) =>
    Assistant.AskEndpoint(body, AssistantConfig.FromEnv(), Assistant.LocalGet($"http://127.0.0.1:{ctx.Connection.LocalPort}")));

app.MapGet("/api/selftest", (HttpContext ctx) => Results.Ok(SelfTest.Run($"http://127.0.0.1:{ctx.Connection.LocalPort}", app.Environment.WebRootPath)));

app.Run();
