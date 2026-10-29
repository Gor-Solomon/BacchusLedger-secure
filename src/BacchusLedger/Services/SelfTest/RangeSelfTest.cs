using BacchusLedger.Dataset;
using BacchusLedger.Providers;
using BacchusLedger.Repositories;

namespace BacchusLedger.Services.SelfTest;

/// Range alert rules (plain code, run on a simulator in a rolled-back transaction).
public sealed class RangeSelfTest(IRangeConfigSource rangeConfig, ILiveSimulatorFactory simulators, IDiagnosticsRepository diagnostics, IUnitOfWork unitOfWork) : ISelfTestSection
{
    public async Task RunAsync(SelfTestContext ctx)
    {
        const string g = "range alerts";
        var Add = ctx.Add;
        try
        {
            var cfg = rangeConfig.Config;
            string R(string metric, bool ferm, string color) => Ranges.For(cfg, "W01", "X-0", metric, ferm, color, 1000) is { } r ? $"{Ranges.Fmt(r.Min)}–{Ranges.Fmt(r.Max)}" : "none";
            var act = $"red {R("temperature", true, "red")}, white {R("temperature", true, "white")}, storage {R("temperature", false, "red")}, pH {R("ph", false, "red")}";
            const string exp = "red 20–30, white 12–20, storage 10–18, pH 3–4";
            Add(g, "config/ranges.json defaults (fermenting red/amber, white, storage, pH)", exp, act, act == exp);
            var amber = Ranges.WineColor("Qvevri amber wine");
            Add(g, "Qvevri amber wine uses the red fermentation range", "red", amber, amber == "red" && Ranges.WineColor("Classic white wine") == "white");

            var own = System.Text.Json.Nodes.JsonNode.Parse("""{"defaults":{"temperature_fermenting_red":{"min":20,"max":30}},"vessels":{"W01/F-2":{"temperature":{"min":22,"max":28}}}}""");
            var o = Ranges.For(own, "W01", "F-2", "temperature", true, "red", null);
            var d = Ranges.For(own, "W01", "F-1", "temperature", true, "red", null);
            Add(g, "Per-vessel override wins over the default", "F-2 22–28, F-1 20–30",
                $"F-2 {o?.Min}–{o?.Max}, F-1 {d?.Min}–{d?.Max}", o is { Min: 22, Max: 28 } && d is { Min: 20, Max: 30 });

            // 3 in a row, no repeat, clear when back in range; invalid values neither count nor reset
            var t = new RangeTracker();
            var range = new Ranges.Range(20, 30, "°C", "test");
            double?[] seq = [31, 31, 25, 31, null, 31, double.NaN, 999, 31, 32, 33, 25, 19, 18, 17];
            var log = seq.Select((v, i) => t.Check("temperature", v, range, null, $"t{i}")).Select(x => x.change == RangeTracker.Change.None ? "." : x.change == RangeTracker.Change.Fire ? "F" : "C").Aggregate("", (a, x) => a + x);
            const string expLog = "........F..C..F";
            Add(g, "Alert after 3 out-of-range readings in a row, no repeat, clear when back in range, empty/impossible values skipped", expLog, log, log == expLog);

            // Simulate spike (= the "Hot fermentation" scenario) on the real simulator, rolled back
            var (fires, before, onlyLive) = await unitOfWork.ProbeAsync(async () =>
            {
                async Task<string> Counts()
                {
                    var c = new List<long>();
                    foreach (var x in new[] { "operations", "readings", "lab", "batches", "vessels" }) c.Add(await diagnostics.CountRowsAsync(x));
                    return string.Join(",", c);
                }
                var c0 = await Counts();
                var sim = simulators.Create(seed: 7);
                sim.On = true;
                await sim.StartAsync(SettingDefaults.Copy());
                await sim.SetScenarioAsync("hot");
                int fired = 0, outBefore = 0, run = 0;
                var max = sim.AllowedMax("F-2", "temperature")!.Value;
                for (int i = 0; i < 15; i++)
                    foreach (var e in (await sim.TickAsync()).Where(e => e.VesselCode == "F-2"))
                    {
                        if (e.Kind == "reading" && e.Metric == "temperature") run = e.Value > max ? run + 1 : 0;
                        if (e.Kind == "notification" && e.Rule == "temp_high") { fired++; if (fired == 1) outBefore = run; }
                    }
                return (fired, outBefore, await Counts() == c0);
            });
            Add(g, "Simulate spike: F-2 too hot fires exactly once, after ≥3 out-of-range readings", "1 alert after ≥3", $"{fires} alert(s) after {before}", fires == 1 && before >= RangeTracker.InARow);
            Add(g, "Simulate spike writes no record rows (only SIMULATED live_events)", "unchanged", onlyLive ? "unchanged" : "changed", onlyLive);
        }
        catch (Exception e) { Add(g, "range alerts", "ok", e.Message, false); }
    }
}
