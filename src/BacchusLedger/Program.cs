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
static Dictionary<string, object?>? BatchRow(SqliteConnection c, long id) =>
    Db.Query(c, "SELECT b.*, w.code AS winery_code, w.name AS winery_name FROM batches b JOIN wineries w ON w.id=b.winery_id WHERE b.id=$1", id).FirstOrDefault();

/// Find a vessel by internal code or by its cellar label (what is written on the tape in the cellar).
static Dictionary<string, object?>? VesselRow(SqliteConnection c, string code, string? winery) =>
    Db.Query(c, """
        SELECT v.*, w.code AS winery_code FROM vessels v JOIN wineries w ON w.id=v.winery_id
        WHERE (v.code=$1 OR (COALESCE(v.cellar_label,'')<>'' AND lower(trim(v.cellar_label))=lower(trim($1)))) AND ($2 IS NULL OR w.code=$2)
        ORDER BY v.code=$1 DESC, v.id
        """, code, winery).FirstOrDefault();

static double? JNum(JsonElement b, string k) => b.TryGetProperty(k, out var v) ? v.ValueKind switch
{
    JsonValueKind.Number => v.GetDouble(),
    JsonValueKind.String when double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
    _ => null,
} : null;
static string? JStr(JsonElement b, string k) => b.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() : null;
static bool JBool(JsonElement b, string k) => b.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;

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

// ---------------- readings (Update 2) ----------------
(List<Readings.Input>? items, string? error) ReadingInputs(SqliteConnection c, JsonElement body)
{
    if (JNum(body, "batch_id") is not { } bid || BatchRow(c, (long)bid) is not { } b) return (null, "Choose a batch");
    long? vesselId = null;
    if (JStr(body, "vessel") is { Length: > 0 } vc)
    {
        var v = VesselRow(c, vc, (string)b["winery_code"]!);
        if (v == null) return (null, $"Vessel {vc} not found at {b["winery_code"]}");
        vesselId = (long)v["id"]!;
    }
    var takenAt = JStr(body, "taken_at") is { Length: >= 16 } ta ? ta[..16] : DateTime.Now.ToString("yyyy-MM-ddTHH:mm");
    var source = JBool(body, "live_demo") ? "Entered (live demo)" : "Entered";
    if (!body.TryGetProperty("readings", out var arr) || arr.ValueKind != JsonValueKind.Array) return (null, "No readings");
    var list = new List<Readings.Input>();
    foreach (var it in arr.EnumerateArray())
    {
        if (JNum(it, "value") is not { } val) continue; // empty field = not measured
        var r = new Readings.Input((long)bid, vesselId, takenAt, JStr(it, "type") ?? "", val,
            JStr(body, "instrument") is { Length: > 0 } ins ? ins : null, JStr(body, "person"), JStr(body, "note"), source);
        if (Readings.Validate(r) is { } err) return (null, err);
        list.Add(r);
    }
    return list.Count == 0 ? (null, "Enter a value") : (list, null);
}

static object ReadingResult(SqliteConnection c, Readings.Input r, List<string> warnings, long? id = null)
{
    // ABV entered together with density uses the Initial Brix of the same vessel/batch
    double? brix = r.Type == "initial_brix" ? r.Value : Readings.LatestBrix(c, r.BatchId, r.VesselId);
    return new { id, type = r.Type, value = r.Value, unit = Readings.Units[r.Type], calc = Readings.Calc(r.Type, r.Value, brix), warnings, source = r.Source };
}

app.MapGet("/api/readings", (long? batch, string? vessel, string? winery) =>
{
    using var c = Db.Open();
    long? vid = null;
    if (!string.IsNullOrEmpty(vessel))
    {
        if (VesselRow(c, vessel, winery) is not { } v) return Results.NotFound(new { error = "vessel not found" });
        vid = (long)v["id"]!;
    }
    return Results.Ok(Readings.List(c, batch, vid));
});

app.MapPost("/api/readings/preview", (JsonElement body) =>
{
    using var c = Db.Open();
    var (items, err) = ReadingInputs(c, body);
    if (items == null) return Results.BadRequest(new { error = err });
    return Results.Ok(new { items = items.Select(r => ReadingResult(c, r, Readings.Warnings(c, r))).ToList() });
});

app.MapPost("/api/readings", (JsonElement body) =>
{
    string? error = null;
    var saved = Db.Write((c, tx) =>
    {
        var (items, err) = ReadingInputs(c, body);
        if (items == null) { error = err; return []; }
        return items.Select(r => { var (id, w) = Readings.Insert(c, tx, r); return ReadingResult(c, r, w, id); }).ToList();
    });
    return error != null ? Results.BadRequest(new { error }) : Results.Ok(new { saved });
});

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

// Which of the 8 journals have rows (for the coverage tiles)
app.MapGet("/api/journals/coverage", (long? batch, string? winery) =>
{
    using var c = Db.Open();
    return Results.Ok(Enumerable.Range(1, 8).Select(n =>
    {
        var j = Journals.Build(c, n, batch, winery);
        return new { n, title = j.Title, rows = j.Rows.Count, filled = j.Rows.Count > 0, template = n == 8 };
    }).ToList());
});

// ---------------- journals ----------------
app.MapGet("/api/journals/{n:int}", (int n, long? batch, string? winery) =>
{
    if (n is < 1 or > 8) return Results.NotFound();
    using var c = Db.Open();
    return Results.Ok(Journals.Build(c, n, batch, winery));
});
app.MapGet("/api/journals/{n:int}.csv", (int n, long? batch, string? winery) =>
{
    if (n is < 1 or > 8) return Results.NotFound();
    using var c = Db.Open();
    var j = Journals.Build(c, n, batch, winery);
    return Results.File(Journals.Csv(j), "text/csv; charset=utf-8", $"journal-{n}-{j.Title.ToLowerInvariant().Replace(' ', '-')}.csv");
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

// ---------------- selftest (sections 5 and 6) ----------------
// Ledger report (read-only): batch from vineyard to bottle as a double-entry ledger; vessel=W01/F-2 adds the SIMULATED live section.
app.MapGet("/api/report", (string? batch, string? vessel) =>
{
    using var c = Db.Open();
    return LedgerReport.Report(c, string.IsNullOrWhiteSpace(batch) ? null : batch, string.IsNullOrWhiteSpace(vessel) ? null : vessel) is { } r
        ? Results.Ok(r) : Results.NotFound(new { error = "batch not found" });
});

// Secure copy: seal (hash chain + DEMO ECDSA signature), verify, and a hidden demo tamper action.
static long? BatchIdByCode(Microsoft.Data.Sqlite.SqliteConnection c, string? code) =>
    code == null ? null : Db.Query(c, "SELECT id FROM batches WHERE code=$1", code).FirstOrDefault()?["id"] as long?;
app.MapPost("/api/seal", (HttpContext ctx, string? batch) =>
{
    var id = Db.Write((c, tx) => { Db.Exec(c, tx, Seal.Tables); var i = BatchIdByCode(c, batch); if (i != null) Seal.Ensure(c, tx, i.Value); return i; });
    if (id == null) return Results.NotFound(new { error = "batch not found" });
    using var c = Db.Open();
    var s = Db.Query(c, "SELECT * FROM seal_batch WHERE batch_id=$1", id)[0];
    var url = $"{ctx.Request.Scheme}://{ctx.Request.Host}/verify.html?batch={Uri.EscapeDataString(batch!)}";
    using var qr = new QRCoder.QRCodeGenerator();
    var png = new QRCoder.PngByteQRCode(qr.CreateQrCode(url, QRCoder.QRCodeGenerator.ECCLevel.M)).GetGraphic(6);
    return Results.Ok(new { batch, final_hash = s["final_hash"], signature = s["signature"], sealed_at = s["sealed_at"], entries = s["entries"], key_id = s["key_id"],
        algorithm = "SHA-256 hash chain · ECDSA P-256 (DEMO KEY)", verify_url = url, chain = Seal.Entries(c, id.Value), qr_png = "data:image/png;base64," + Convert.ToBase64String(png) });
});
app.MapGet("/api/verify", (string? batch) =>
{
    using var c = Db.Open();
    var id = BatchIdByCode(c, batch);
    if (id == null) return Results.NotFound(new { error = "batch not found" });
    return Results.Ok(new { batch, result = Seal.Verify(c, id.Value), public_key = Seal.PublicKeyPem, key_note = "DEMO KEY — not for production" });
});
app.MapPost("/api/demo/reset-seal", (string? batch) =>
{
    var r = Db.Write((c, tx) => { Db.Exec(c, tx, Seal.Tables); return BatchIdByCode(c, batch) is { } id ? Seal.ResetDemo(c, tx, id) : null; });
    return r == null ? Results.NotFound(new { error = "batch not found" }) : Results.Ok(r);
});
app.MapPost("/api/demo/tamper", (string? batch) =>
{
    var t = Db.Write((c, tx) => BatchIdByCode(c, batch) is { } id ? Seal.TamperDemo(c, tx, id) : null);
    return t == null ? Results.BadRequest(new { error = "no sealed lab entry to change" }) : Results.Ok(t);
});

app.MapGet("/api/selftest", (HttpContext ctx) => Results.Ok(SelfTest.Run($"http://127.0.0.1:{ctx.Connection.LocalPort}", app.Environment.WebRootPath)));

app.Run();
