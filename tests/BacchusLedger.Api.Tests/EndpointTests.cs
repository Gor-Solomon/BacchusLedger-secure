using System.Net;
using System.Text.Json.Nodes;

namespace BacchusLedger.Api.Tests;

public class EndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    async Task<JsonNode> GetJson(string path)
    {
        var resp = await factory.CreateClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
    }

    [Fact]
    public async Task Overview_shows_the_cellar_totals_and_the_two_W03_flags()
    {
        var o = await GetJson("/api/overview");
        Assert.Equal(20, (double)o["kpi"]!["wine_in_cellar_l"]!);
        Assert.Equal(5, (int)o["kpi"]!["batches"]!);
        Assert.Equal(498, (double)o["kpi"]!["total_loss_l"]!);
        Assert.Equal(1489.02, (double)o["kpi"]!["loss_value_usd"]!);
        var above = o["flags"]!.AsArray().Where(f => (string?)f!["level"] == "above").Select(f => $"{f!["batch"]} {f["stage"]}");
        Assert.Equal(["W03-B3-2025 filtration", "W03-B3-2025 bottling"], above);
    }

    [Fact]
    public async Task Batch_W01_has_450_litres_loss_and_its_four_farmers()
    {
        var b = await GetJson("/api/batches/1");
        Assert.Equal("W01-B1-2025", (string?)b["batch"]!["code"]);
        Assert.Equal(450, (double)b["totals"]!["total_loss_l"]!);
        Assert.Equal(9630, (double)b["totals"]!["bottled_l"]!);
        Assert.Equal(4, b["origins"]!.AsArray().Count);
        Assert.Equal("racking_first", (string?)b["stages"]![2]!["stage_key"]);
    }

    [Fact]
    public async Task Unknown_batch_is_404_with_an_error()
    {
        var resp = await factory.CreateClient().GetAsync("/api/batches/999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal("""{"error":"batch not found"}""", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Selftest_returns_all_pass()
    {
        var s = await GetJson("/api/selftest");
        var failed = s["checks"]!.AsArray().Where(c => !(bool)c!["pass"]!).Select(c => $"{c!["group"]}: {c["name"]} → {c["actual"]}").ToList();
        Assert.Empty(failed);
        Assert.True((bool)s["pass"]!);
        Assert.Equal(81, (int)s["total"]!);
    }
}
