using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using BacchusLedger.Application.Events;
using BacchusLedger.Application.Messaging;
using BacchusLedger.Application.Providers;
using BacchusLedger.Application.Services;
using BacchusLedger.Infrastructure.Outbox;
using BacchusLedger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BacchusLedger.Api.Tests;

/// The event pipeline end to end, with the real app on a real SQLite file: producer → outbox → worker → handlers → SSE / notifications.
public class OutboxPipelineTests
{
    /// Fast worker for tests: poll every 50 ms, retries after 20 ms, 40 ms, 80 ms …
    static readonly Dictionary<string, string> Fast = new() { ["Outbox:PollSeconds"] = "0.05", ["Outbox:RetryBaseSeconds"] = "0.02" };

    static readonly object Dip = new { vessel = "F-2", dip_cm = 100 };

    static async Task Until(Func<Task<bool>> condition, string what, int seconds = 20)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (!await condition())
        {
            if (DateTime.UtcNow > end) throw new TimeoutException($"waited {seconds} s for: {what}");
            await Task.Delay(50);
        }
    }

    static async Task<T> Db<T>(ApiFactory f, Func<BacchusLedgerDbContext, Task<T>> query)
    {
        using var scope = f.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<BacchusLedgerDbContext>());
    }

    static Task<List<Infrastructure.Persistence.Entities.OutboxMessageEntity>> Messages(ApiFactory f, string type) =>
        Db(f, db => db.OutboxMessages.AsNoTracking().Where(m => m.Type == type).ToListAsync());

    /// Every SSE message published from now on (as the open streams get them).
    static Func<List<string>> Listen(ApiFactory f)
    {
        var (reader, _) = f.Services.GetRequiredService<ILiveHub>().Subscribe();
        var got = new List<string>();
        return () => { while (reader.TryRead(out var m)) got.Add(m); return got; };
    }

    [Fact]
    public async Task Sensor_readings_above_the_range_are_saved_and_the_worker_raises_a_real_notification()
    {
        // F-2 may hold at most 5,000 L here; a 100 cm dip is 7,894 L, so every reading is "too high"
        using var f = new ApiFactory(null, Fast, s => s.AddSingleton<IRangeConfigSource>(new FixedRanges(
            """{"defaults":{"level":{"min":0,"max":"capacity"}},"vessels":{"W01/F-2":{"level":{"min":0,"max":5000}}}}""")));
        var client = f.CreateClient();
        var sse = Listen(f);
        var before = await Db(f, db => db.SensorReadings.CountAsync());

        var resp = await client.PostAsJsonAsync("/api/sensors/reading", Dip);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(before + 1, await Db(f, db => db.SensorReadings.CountAsync()));
        var message = Assert.Single(await Messages(f, nameof(SensorReadingReceived)));
        Assert.Equal("F-2", message.Vessel);
        await Until(async () => (await Messages(f, nameof(SensorReadingReceived)))[0].Status == OutboxStatus.Processed, "message processed");
        var pushed = Assert.Single(sse(), m => m.StartsWith("event: sensor"));
        Assert.Contains("\"vessel_code\":\"F-2\"", pushed);
        Assert.Contains("\"source\":\"SENSOR\"", pushed);
        Assert.Equal(1, await Db(f, db => db.ProcessedMessages.CountAsync(p => p.MessageId == message.Id && p.Handler == "LiveStreamHandler")));
        Assert.Equal(1, await Db(f, db => db.ProcessedMessages.CountAsync(p => p.MessageId == message.Id && p.Handler == "RangeRulesHandler")));

        // like the simulator's range alerts: one red alert after 3 readings in a row out of range
        Assert.Empty((await client.GetFromJsonAsync<JsonArray>("/api/live/events?kind=notification"))!);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/sensors/reading", Dip)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/sensors/reading", Dip)).StatusCode);
        await Until(async () => (await client.GetFromJsonAsync<JsonArray>("/api/live/events?kind=notification"))!.Count > 0, "sensor notification");

        var note = Assert.Single((await client.GetFromJsonAsync<JsonArray>("/api/live/events?kind=notification"))!)!;
        Assert.Equal("red", (string?)note["level"]);
        Assert.Equal("level_high", (string?)note["rule"]);
        Assert.Equal("SENSOR", (string?)note["source"]);
        Assert.Equal("F-2", (string?)note["vessel_code"]);
        Assert.Equal("F-2: level too high — 7894 L (allowed 0–5000 L, 3 readings in a row).", (string?)note["message"]);
        await Until(() => Task.FromResult(sse().Any(m => m.StartsWith("event: tick") && m.Contains("\"rule\":\"level_high\""))), "notification on the live stream");
        Assert.Single(await Messages(f, nameof(RangeAlertRaised)));

        // a fourth reading still out of range does not repeat the alert
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/sensors/reading", Dip)).StatusCode);
        await Until(async () => (await Messages(f, nameof(SensorReadingReceived))).All(m => m.Status == OutboxStatus.Processed), "all processed");
        Assert.Single((await client.GetFromJsonAsync<JsonArray>("/api/live/events?kind=notification"))!);
    }

    sealed class FixedRanges(string json) : IRangeConfigSource
    {
        public JsonNode? Config { get; } = JsonNode.Parse(json);
    }

    [Fact]
    public async Task Simulated_hot_fermentation_raises_the_range_alert_through_the_worker()
    {
        using var f = new ApiFactory(null, new Dictionary<string, string>(Fast) { ["Live:TickSeconds"] = "1" });
        var client = f.CreateClient();
        var sse = Listen(f);

        await client.PostAsync("/api/live/scenario/hot", null);
        await Until(async () => (await client.GetFromJsonAsync<JsonArray>("/api/live/events?kind=notification"))!
            .Any(n => (string?)n!["rule"] == "temp_high"), "temp_high notification", seconds: 60);
        await client.PostAsync("/api/live/off", null);

        await Until(() => Task.FromResult(sse().Any(m => m.StartsWith("event: tick") && m.Contains("\"rule\":\"temp_high\""))), "temp_high on the live stream");
        var alert = (await Messages(f, nameof(RangeAlertRaised))).Single(m => m.Payload.Contains("temp_high"));
        Assert.Equal("F-2", alert.Vessel);
        Assert.NotEmpty(await Messages(f, nameof(LiveReadingSimulated)));
        Assert.True(await Db(f, db => db.ProcessedMessages.AnyAsync(p => p.Handler == "RangeRulesHandler")));
    }

    [Fact]
    public async Task A_failing_handler_keeps_the_reading_retries_then_fails_and_the_retry_endpoint_processes_it()
    {
        var failing = new SwitchableSensorHandler { Fail = true };
        using var f = new ApiFactory(null, Fast, s => s.AddSingleton<IEventHandler<SensorReadingReceived>>(failing));
        var client = f.CreateClient();
        var sse = Listen(f);
        var before = await Db(f, db => db.SensorReadings.CountAsync());

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/sensors/reading", Dip)).StatusCode);
        Assert.Equal(before + 1, await Db(f, db => db.SensorReadings.CountAsync())); // the reading is saved whatever the handlers do

        await Until(async () => (await Messages(f, nameof(SensorReadingReceived)))[0].Status == OutboxStatus.Failed, "message failed");
        var message = (await Messages(f, nameof(SensorReadingReceived)))[0];
        Assert.Equal(5, message.Attempts);
        Assert.Contains("SwitchableSensorHandler: sensor handler is down", message.LastError);
        Assert.Equal(5, failing.Runs);
        var failed = (await client.GetFromJsonAsync<JsonArray>("/api/outbox/failed"))!;
        Assert.Equal(message.Id.ToString(), (string?)failed[0]!["id"]);
        Assert.Equal(1, (long)(await client.GetFromJsonAsync<JsonObject>("/api/outbox/stats"))!["failed"]!);

        failing.Fail = false;
        var retry = await client.PostAsync($"/api/outbox/failed/{message.Id}/retry", null);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        await Until(async () => (await Messages(f, nameof(SensorReadingReceived)))[0].Status == OutboxStatus.Processed, "message processed after retry");

        Assert.Equal(6, failing.Runs);
        Assert.Single(sse(), m => m.StartsWith("event: sensor")); // LiveStreamHandler succeeded the first time and never ran again
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/outbox/failed/{message.Id}/retry", null)).StatusCode);
    }

    [Fact]
    public async Task Messages_left_pending_while_the_worker_was_stopped_are_processed_when_the_app_starts_again()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bacchus-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using (var stopped = new ApiFactory(dir, Fast, s => s.Remove(s.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(OutboxProcessor)))))
            {
                var client = stopped.CreateClient();
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/sensors/reading", Dip)).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/sensors/reading", Dip)).StatusCode);
                await Task.Delay(300);
                Assert.All(await Messages(stopped, nameof(SensorReadingReceived)), m => Assert.Equal(OutboxStatus.Pending, m.Status));
                Assert.Equal(2, (await Messages(stopped, nameof(SensorReadingReceived))).Count);
            }

            using var restarted = new ApiFactory(dir, Fast);
            restarted.CreateClient();
            await Until(async () => (await Messages(restarted, nameof(SensorReadingReceived))).All(m => m.Status == OutboxStatus.Processed), "both processed after start");
            Assert.Equal(2, (await Messages(restarted, nameof(SensorReadingReceived))).Count);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public async Task When_the_outbox_row_cannot_be_saved_the_reading_is_not_saved_either()
    {
        using var f = new ApiFactory(null, Fast, s =>
        {
            s.AddScoped<EfOutbox>();
            s.AddScoped<IOutbox>(sp => new DuplicatingOutbox(sp.GetRequiredService<EfOutbox>()));
        });
        var client = f.CreateClient();
        var readings = await Db(f, db => db.SensorReadings.CountAsync());
        var events = await Db(f, db => db.LiveEvents.CountAsync());

        HttpStatusCode? status = null;
        try { status = (await client.PostAsJsonAsync("/api/sensors/reading", Dip)).StatusCode; }
        catch (Exception) { } // the test server may rethrow the server error instead of answering 500
        Assert.NotEqual(HttpStatusCode.OK, status);

        Assert.Equal(readings, await Db(f, db => db.SensorReadings.CountAsync()));
        Assert.Equal(events, await Db(f, db => db.LiveEvents.CountAsync()));
        Assert.Equal(0, await Db(f, db => db.OutboxMessages.CountAsync()));
    }

    sealed class SwitchableSensorHandler : IEventHandler<SensorReadingReceived>
    {
        public bool Fail { get; set; }
        public int Runs { get; private set; }

        public Task HandleAsync(SensorReadingReceived e, CancellationToken ct)
        {
            Runs++;
            return Fail ? throw new InvalidOperationException("sensor handler is down") : Task.CompletedTask;
        }
    }

    /// Saves each event twice: the second insert breaks the primary key, so the database refuses the outbox row.
    sealed class DuplicatingOutbox(IOutbox inner) : IOutbox
    {
        public async Task EnqueueAsync(IOutboxEvent e)
        {
            await inner.EnqueueAsync(e);
            await inner.EnqueueAsync(e);
        }
    }
}
