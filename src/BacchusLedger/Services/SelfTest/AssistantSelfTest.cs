using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BacchusLedger.Dataset;
using BacchusLedger.Options;
using BacchusLedger.Providers;
using BacchusLedger.Repositories;
using BacchusLedger.Services.Assistant;
using Microsoft.Extensions.Options;
using static BacchusLedger.Services.Assistant.AssistantTexts;

namespace BacchusLedger.Services.SelfTest;

/// Assistant: quick answers, no-key behaviour, tools, key leak check and read-only check. Never calls an AI provider.
public sealed class AssistantSelfTest(
    IQuickAnswerService quick, IAssistantService assistant, IAssistantTools tools, ILocalApiClient api, IDiagnosticsRepository diagnostics,
    IOptions<AssistantOptions> options) : ISelfTestSection
{
    public async Task RunAsync(SelfTestContext ctx)
    {
        const string g = "assistant";
        var Add = ctx.Add;
        var s = SettingDefaults.Copy();
        string? before = null;
        try { before = await diagnostics.SnapshotAsync(); } catch (Exception e) { Add(g, "database snapshot", "ok", e.Message, false); }

        try
        {
            var w = await quick.W01LossesAsync(s);
            var act = w == null ? "missing" : string.Join(", ", w.Steps.Select(x => $"{x.Stage} {F(x.LossL)}"));
            const string exp = "racking_first 280, stock_check 0, racking 100, filtration 50, bottling 20";
            Add(g, "Quick question 1: W01 loss per step", exp, act, act == exp);
            Add(g, "Quick question 1: W01 total 450 L + 20 L remainder, $1,345.50", "450 L + 20 L, $1,345.50",
                w == null ? "missing" : $"{F(w.TotalLossL)} L + {F(w.RemainderL)} L, ${F(w.ValueUsd, 2)}",
                w != null && Math.Abs(w.TotalLossL - 450) < 0.01 && Math.Abs(w.RemainderL - 20) < 0.01 && Math.Abs(w.ValueUsd - 1345.50) < 0.001);
            var q1 = (await quick.AnswerAsync(1, "en", s)).Text;
            Add(g, "Quick question 1 answer text shows 450 L", "contains 450 L", q1.Contains("450 L") ? "contains 450 L" : q1, q1.Contains("450 L"));

            var flags = await quick.AboveNormalAsync(s);
            var fact = string.Join("; ", flags.Select(f => $"{f.Batch} {f.Stage} {F(f.LossPct, 2)}%"));
            const string fexp = "W03-B3-2025 filtration 1.00%; W03-B3-2025 bottling 0.51%";
            Add(g, "Quick question 2: exactly the two W03 flagged stages", fexp, fact.Length == 0 ? "none" : fact, fact == fexp);
        }
        catch (Exception e) { Add(g, "quick questions", "ok", e.Message, false); }

        // Ask with no key configured: HTTP 200 (the endpoint always answers) + "AI not configured", no crash.
        var bodies = new List<(string where, string text)>();
        const string noKey = "POST /api/assistant/ask with no key: HTTP 200 + \"AI not configured\"";
        try
        {
            var a = await assistant.AskAsync("How much wine did W01 lose?", AssistantOptions.None, _ => Task.FromResult<JsonNode?>(null));
            bodies.Add(("ask (no key)", JsonSerializer.Serialize(a)));
            Add(g, noKey, "200 · AI not configured", $"200 · {a.Text}", a.Text.Contains("AI not configured"));
        }
        catch (Exception e) { Add(g, noKey, "200", e.Message, false); }

        // Every tool answers from our own API (read-only GETs).
        if (ctx.BaseUrl is { } baseUrl)
        {
            try
            {
                var calls = new (string name, string args)[]
                {
                    ("get_overview", "{}"), ("list_batches", "{}"), ("get_batch", """{"code":"W01-B1-2025"}"""),
                    ("get_readings", """{"batch_or_vessel":"W03-B3-2025","type":"ph"}"""), ("get_vessel", """{"cellar_label_or_code":"F-1"}"""),
                    ("get_journal", """{"n":5,"batch":"W01-B1-2025"}"""), ("get_live_alerts", "{}"), ("search_facts", """{"query":"exports"}"""),
                };
                var bad = new List<string>();
                foreach (var (name, args) in calls)
                {
                    var (json, _) = await tools.RunAsync(name, (JsonObject)JsonNode.Parse(args)!, path => api.GetJsonAsync(baseUrl, path));
                    bodies.Add(($"tool {name}", json));
                    if (JsonNode.Parse(json) is JsonObject o && o["error"] != null) bad.Add($"{name}: {o["error"]}");
                }
                Add(g, $"All {calls.Length} assistant tools return ledger data", "no errors", bad.Count == 0 ? "no errors" : string.Join("; ", bad), bad.Count == 0);
            }
            catch (Exception e) { Add(g, "assistant tools", "no errors", e.Message, false); }
        }

        // Key leak check (needles are built at run time so this check's own output never contains them).
        var needles = new List<string> { "s" + "k-", "A" + "Q." };
        needles.AddRange(options.Value.Keys());
        var hits = new List<string>();
        int files = 0;
        if (ctx.WebRoot != null && Directory.Exists(ctx.WebRoot))
            foreach (var f in Directory.EnumerateFiles(ctx.WebRoot, "*", SearchOption.AllDirectories))
            {
                files++;
                var text = Encoding.Latin1.GetString(await File.ReadAllBytesAsync(f));
                if (needles.Any(n => text.Contains(n, StringComparison.Ordinal))) hits.Add(Path.GetRelativePath(ctx.WebRoot, f));
            }
        int responses = 0;
        if (ctx.BaseUrl != null)
        {
            string[] paths = ["/health", "/api/overview", "/api/batches", "/api/vessels", "/api/settings", "/api/readings", "/api/journals/coverage", "/api/live/state",
                "/api/live/events", "/api/assistant/status", "/api/assistant/status?lang=ka", "/api/assistant/quick/1", "/api/assistant/quick/2", "/api/assistant/quick/3",
                "/api/assistant/quick/4", "/api/assistant/quick/5", "/api/assistant/quick/1?lang=ka"];
            foreach (var p in paths)
            {
                try { bodies.Add((p, await api.GetStringAsync(ctx.BaseUrl, p))); }
                catch (Exception e) { hits.Add($"{p} ({e.GetType().Name})"); }
            }
            try
            {
                // empty question: answered without calling any AI provider
                var (status, body) = await api.PostJsonAsync(ctx.BaseUrl, "/api/assistant/ask", """{"question":""}""");
                bodies.Add(("POST /api/assistant/ask", body));
                if (status != 200) hits.Add($"POST /api/assistant/ask HTTP {status}");
            }
            catch (Exception e) { hits.Add($"POST /api/assistant/ask ({e.GetType().Name})"); }
        }
        foreach (var (where, text) in bodies)
        {
            responses++;
            if (needles.Any(n => text.Contains(n, StringComparison.Ordinal))) hits.Add(where);
        }
        Add(g, $"Key leak check: {files} served files and {responses} API responses contain no key or key prefix", "none",
            hits.Count == 0 ? "none" : string.Join(", ", hits), hits.Count == 0 && files > 0 && responses > 0);

        // Read-only: nothing above wrote to the database.
        try
        {
            for (int n = 1; n <= 5; n++) await quick.AnswerAsync(n, "ka", s);
            var after = await diagnostics.SnapshotAsync();
            Add(g, "Assistant is read-only: no database writes during quick questions, tools and ask", "unchanged", before == after ? "unchanged" : "changed", before != null && before == after);
        }
        catch (Exception e) { Add(g, "Assistant is read-only", "unchanged", e.Message, false); }
    }
}
