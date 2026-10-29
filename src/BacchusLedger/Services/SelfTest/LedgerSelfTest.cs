using System.Text.Json;
using System.Text.RegularExpressions;
using BacchusLedger.Dataset;
using BacchusLedger.Repositories;

namespace BacchusLedger.Services.SelfTest;

/// The ledger of the demo batches balances, and building it changes nothing.
public sealed class LedgerSelfTest(
    IBatchRepository batches, IDiagnosticsRepository diagnostics, IUnitOfWork unitOfWork, IReportService reports, IDemoService demo,
    ILiveSimulatorFactory simulators, ILiveHub hub) : ISelfTestSection
{
    public async Task RunAsync(SelfTestContext ctx)
    {
        const string g = "ledger report";
        var Add = ctx.Add;
        try
        {
            foreach (var (code, inL, outL, loss) in new[] { (SeedDataset.W01Batch, 10100.0, 9650.0, 450.0), (SeedDataset.W03Batch, 1230.0, 1182.0, 48.0) })
            {
                if (await batches.FindIdAsync(code) is not { } id) { Add(g, $"{code} ledger balances", "present", "missing", false); continue; }
                var b = (await reports.LedgerAsync(id))!.Balance;
                Add(g, $"{code} ledger balances: in = out + losses, no unexplained difference",
                    $"{inL:N0} = {outL:N0} + {loss:N0}, gap 0", $"{b.InL:N0} = {b.BottledL + b.InVesselsL:N0} + {b.LossesL:N0}, gap {b.GapL:0.##}",
                    b.Balanced && b.InL == inL && b.BottledL + b.InVesselsL == outL && b.LossesL == loss);
            }

            // Run both demo players to the end in a rolled-back transaction, then check the demo copies' ledgers.
            var replayed = await unitOfWork.ProbeAsync(async () =>
            {
                var r = new List<string>();
                foreach (var script in new[] { "w01", "w03" })
                {
                    await demo.ReplayAllAsync(script);
                    var code = script == "w03" ? DemoService.W03Code : DemoService.W01Code;
                    var b = (await reports.LedgerAsync((await batches.FindIdAsync(code))!.Value))!.Balance;
                    r.Add($"{code} {(b.Balanced ? "balanced" : $"gap {b.GapL} L")}");
                }
                return r;
            });
            Add(g, "Demo copies (A→Z and Qvevri players run to the end) balance", "both balanced", string.Join(", ", replayed), replayed.All(x => x.EndsWith("balanced")));

            // Simulated live events never enter the ledger; building the report writes nothing.
            var (same, sim, unchanged) = await unitOfWork.ProbeAsync(async () =>
            {
                async Task<string> Counts()
                {
                    var c = new List<long>();
                    foreach (var t in new[] { "operations", "readings", "lab", "batches", "deliveries", "batch_origins", "live_events" }) c.Add(await diagnostics.CountRowsAsync(t));
                    return string.Join(",", c);
                }
                async Task<string> Ledger(string? vessel) => hub.ToJson((await reports.BuildAsync(SeedDataset.W03Batch, vessel))!);
                var before = await Ledger(null);
                var sim0 = simulators.Create(seed: 3);
                sim0.On = true;
                await sim0.StartAsync(SettingDefaults.Copy());
                await sim0.SetScenarioAsync("hot");
                for (int i = 0; i < 6; i++) await sim0.TickAsync();
                var c0 = await Counts();
                var withSim = await Ledger("W03/Q-3");
                var after = await Ledger(null);
                return (StripTime(before) == StripTime(after), withSim.Contains("\"source\":\"SIMULATED\""), await Counts() == c0);
            });
            Add(g, "Simulated live events stay out of the ledger (separate SIMULATED section)", "ledger identical", same && sim ? "ledger identical" : "changed", same && sim);
            Add(g, "Building the report writes nothing", "unchanged", unchanged ? "unchanged" : "changed", unchanged);
        }
        catch (Exception e) { Add(g, "ledger report", "ok", e.Message, false); }
    }

    static string StripTime(string json) => Regex.Replace(json, "\"generated_at\":\"[^\"]*\"", "");
}
