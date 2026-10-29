using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using BacchusLedger.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BacchusLedger.Api.Tests;

/// Write requests through EF Core: operations, readings, append-only triggers, demo player, seal / tamper / verify.
public class WritePathTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    async Task<(HttpStatusCode Status, JsonNode? Body)> Send(HttpMethod method, string path, object? body = null)
    {
        var req = new HttpRequestMessage(method, path) { Content = body == null ? null : JsonContent.Create(body) };
        var resp = await factory.CreateClient().SendAsync(req);
        var text = await resp.Content.ReadAsStringAsync();
        return (resp.StatusCode, text.Length == 0 ? null : JsonNode.Parse(text));
    }

    [Fact]
    public async Task Recording_an_operation_adds_a_stage_with_its_loss()
    {
        var (status, saved) = await Send(HttpMethod.Post, "/api/operations",
            new { batch_id = 3, type = "stock_check", from_vessel = "T3-3", litres = 0, date = "2026-06-20", person = "test" });
        Assert.Equal(HttpStatusCode.OK, status);
        var id = (long)saved!["id"]!;
        Assert.Equal(id, (long)saved["stage"]!["op_id"]!);
        Assert.Equal("Entered", (string?)saved["stage"]!["source"]);
        var (_, batch) = await Send(HttpMethod.Get, "/api/batches/3");
        Assert.Contains(batch!["stages"]!.AsArray(), s => (long)s!["op_id"]! == id);
    }

    [Fact]
    public async Task An_invalid_operation_is_refused_with_400()
    {
        var (status, body) = await Send(HttpMethod.Post, "/api/operations", new { batch_id = 1, type = "racking", from_vessel = "NOPE", to_vessel = "T1-4", litres = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("Vessel NOPE not found at W01", (string?)body!["error"]);
    }

    [Fact]
    public async Task Saving_readings_returns_calculations_and_lists_them()
    {
        var (status, saved) = await Send(HttpMethod.Post, "/api/readings", new
        {
            batch_id = 2, taken_at = "2025-09-23T10:00", readings = new object[] { new { type = "initial_brix", value = 22 }, new { type = "hydrometer_abv", value = "6.49" } },
        });
        Assert.Equal(HttpStatusCode.OK, status);
        var items = saved!["saved"]!.AsArray();
        Assert.Equal(12.98, (double)items[0]!["calc"]!["potential_abv"]!);
        Assert.Equal(50.0, (double)items[1]!["calc"]!["progress_pct"]!);
        var (_, list) = await Send(HttpMethod.Get, "/api/readings?batch=2");
        Assert.Contains(list!["rows"]!.AsArray(), r => (long)r!["id"]! == (long)items[0]!["id"]!);
    }

    [Theory]
    [InlineData("operations", false)]
    [InlineData("operations", true)]
    [InlineData("readings", false)]
    [InlineData("readings", true)]
    public async Task Append_only_tables_refuse_update_and_delete(string table, bool delete)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BacchusLedgerDbContext>();
        var ex = await Assert.ThrowsAsync<SqliteException>(() => (table, delete) switch
        {
            ("operations", false) => db.Operations.Where(o => o.Id == 1).ExecuteUpdateAsync(u => u.SetProperty(o => o.Litres, 0)),
            ("operations", true) => db.Operations.Where(o => o.Id == 1).ExecuteDeleteAsync(),
            ("readings", false) => db.Readings.Where(r => r.Id == 1).ExecuteUpdateAsync(u => u.SetProperty(r => r.Value, 0)),
            _ => db.Readings.Where(r => r.Id == 1).ExecuteDeleteAsync(),
        });
        Assert.Contains($"{table} are append-only", ex.Message);
    }

    [Fact]
    public async Task Demo_player_writes_its_steps_and_reset_removes_them()
    {
        var (status, card) = await Send(HttpMethod.Post, "/api/demo/step/7?script=w03");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("W03-B3-2025-DEMO", (string?)card!["batch_code"]);
        Assert.Equal(1182, (double)card["totals"]!["bottled_l"]!);
        var (_, steps) = await Send(HttpMethod.Get, "/api/demo/steps?script=w03");
        Assert.All(steps!["steps"]!.AsArray().Take(8), s => Assert.True((bool)s!["written"]!));

        var (_, reset) = await Send(HttpMethod.Post, "/api/demo/reset?script=w03");
        Assert.All(reset!["steps"]!.AsArray(), s => Assert.False((bool)s!["written"]!));
        var (_, batches) = await Send(HttpMethod.Get, "/api/batches");
        Assert.DoesNotContain(batches!.AsArray(), b => (string?)b!["code"] == "W03-B3-2025-DEMO");
    }

    [Fact]
    public async Task Seal_verify_tamper_and_reset_seal()
    {
        var (status, seal) = await Send(HttpMethod.Post, "/api/seal?batch=W01-B1-2025");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True((long)seal!["entries"]! > 0);
        Assert.StartsWith("data:image/png;base64,", (string?)seal["qr_png"]);

        var (_, ok) = await Send(HttpMethod.Get, "/api/verify?batch=W01-B1-2025");
        Assert.Equal("verified", (string?)ok!["result"]!["status"]);

        var (_, tamper) = await Send(HttpMethod.Post, "/api/demo/tamper?batch=W01-B1-2025");
        var (_, bad) = await Send(HttpMethod.Get, "/api/verify?batch=W01-B1-2025");
        Assert.Equal("tampered", (string?)bad!["result"]!["status"]);
        Assert.Equal((long)tamper!["entry"]!, (long)bad["result"]!["tampered_at"]!);

        var (_, restored) = await Send(HttpMethod.Post, "/api/demo/reset-seal?batch=W01-B1-2025");
        Assert.Equal(1, (int)restored!["restored"]!);
        var (_, again) = await Send(HttpMethod.Get, "/api/verify?batch=W01-B1-2025");
        Assert.Equal("verified", (string?)again!["result"]!["status"]);
    }
}
