using System.Text.Json;
using BacchusLedger.Dataset;
using BacchusLedger.Models;
using BacchusLedger.Repositories;
using BacchusLedger.Services.Assistant;

namespace BacchusLedger.Services.SelfTest;

/// LIVE mode: simulated events stay in live_events and never change the record.
public sealed class LiveSelfTest(
    IUnitOfWork unitOfWork, ILiveSimulatorFactory simulators, ILiveEventRepository liveEvents, IBatchRepository batches, IWineryRepository wineries,
    IDiagnosticsRepository diagnostics, IJournalService journals, ILossAnalysisService losses, IHttpClientFactory httpClients) : ISelfTestSection
{
    /// Everything the record (journals, batch loss totals, ROI) shows: must be identical with Live ON and OFF.
    async Task<string> RecordSnapshotAsync()
    {
        var s = SettingDefaults.Copy();
        var parts = new List<object>();
        for (int n = 1; n <= 8; n++) parts.Add((await journals.BuildAsync(n, null, null)).Rows);
        foreach (var id in await batches.ListIdsAsync())
        {
            var r = await losses.AnalyzeAsync(id, s, compare: false);
            parts.Add(new[] { r.BatchCode, $"{r.TotalLossL}", $"{r.LossValueUsd}", $"{r.BottledL}", $"{r.LitresNow}", $"{r.TotalDifferenceL}" });
        }
        parts.Add(Money.OnePercentTable(await wineries.ListBySizeAsync(), 2.99, 631));
        foreach (var t in new[] { "operations", "readings", "lab", "deliveries", "orders", "batches", "batch_origins", "vessels" })
            parts.Add(await diagnostics.CountRowsAsync(t));
        return JsonSerializer.Serialize(parts);
    }

    public async Task RunAsync(SelfTestContext ctx)
    {
        var Add = ctx.Add;
        try
        {
            var (same, events, phases) = await unitOfWork.ProbeAsync(async () =>
            {
                var before = await RecordSnapshotAsync();
                var sim = simulators.Create(seed: 42);
                sim.On = true;
                await sim.StartAsync(SettingDefaults.Copy());
                var all = new List<LiveEvent>();
                var notes = new Dictionary<string, List<LiveEvent>>();
                foreach (var (scenario, ticks) in new[] { ("hot", 12), ("loss", 5), ("stuck", 12), ("missing", 2), ("allgood", 1) })
                {
                    await sim.SetScenarioAsync(scenario);
                    var ev = new List<LiveEvent>();
                    for (int i = 0; i < ticks; i++) ev.AddRange(await sim.TickAsync());
                    all.AddRange(ev);
                    notes[scenario] = ev.Where(e => e.Kind == "notification").ToList();
                }
                var after = await RecordSnapshotAsync();
                var stored = await liveEvents.CountSimulatedAsync();
                return (before == after && stored >= all.Count, all, notes);
            });
            Add("live", "Live events only in live_events: journals, batch loss totals and ROI identical with Live ON and OFF", "identical", same ? "identical" : "changed", same);
            Add("live", "Every simulated live event has source = 'SIMULATED'", $"{events.Count} SIMULATED", $"{events.Count(e => e.Source == "SIMULATED")} SIMULATED", events.Count > 0 && events.All(e => e.Source == "SIMULATED"));
            var other = await liveEvents.CountUntaggedAsync();
            Add("live", "Stored live events: none without a SIMULATED (or SENSOR stub) tag", "0", $"{other}", other == 0);
            foreach (var (scenario, level, rule) in new[] { ("hot", "red", "temp_high"), ("loss", "red", "level_drop"), ("stuck", "amber", "stuck"), ("missing", "amber", "missing_brix"), ("allgood", "green", "") })
            {
                var hit = phases[scenario].FirstOrDefault(e => e.Level == level && (rule == "" || e.Rule == rule));
                Add("live", $"Scenario \"{LiveScenarios.Titles[scenario]}\" → {level} {(rule == "" ? "back to normal" : rule)}", $"{level} {rule}".Trim(),
                    hit?.Message ?? string.Join(" | ", phases[scenario].Select(e => $"{e.Level} {e.Rule}")), hit != null);
            }
        }
        catch (Exception e) { Add("live", "live simulator probe", "ok", e.Message, false); }

        // SSE endpoint content type
        const string sse = "SSE endpoint /api/live/stream responds with text/event-stream";
        if (ctx.BaseUrl != null)
        {
            try
            {
                var http = httpClients.CreateClient("selftest");
                using var resp = await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, ctx.BaseUrl + "/api/live/stream?once=true"), HttpCompletionOption.ResponseHeadersRead);
                var ctype = resp.Content.Headers.ContentType?.MediaType ?? "none";
                Add("live", sse, "text/event-stream", ctype, ctype == "text/event-stream");
            }
            catch (Exception e) { Add("live", sse, "text/event-stream", e.Message, false); }
        }
        else Add("live", sse, "text/event-stream", "not checked (no server URL)", false);
    }
}
