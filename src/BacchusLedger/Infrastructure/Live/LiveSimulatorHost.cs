using BacchusLedger.Contracts.Live;
using BacchusLedger.Options;
using BacchusLedger.Repositories;
using BacchusLedger.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BacchusLedger.Infrastructure.Live;

/// Background simulator: one tick every LiveOptions.TickSeconds while Live is ON.
public sealed class LiveSimulatorHost(
    ILiveSimulatorFactory factory, ILiveHub hub, IUnitOfWork unitOfWork, ILiveEventRepository events, IOptions<LiveOptions> options,
    ILogger<LiveSimulatorHost> logger) : BackgroundService, ILiveControl
{
    readonly ILiveSimulator sim = factory.Create();
    readonly SemaphoreSlim gate = new(1, 1);

    public LiveStateDto State() => sim.State();

    public void UseSettings(IReadOnlyDictionary<string, string> settings) => sim.UseSettings(settings);

    public async Task TurnOnAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (sim.On) return;
            await sim.StartAsync();
            sim.On = true;
        }
        finally { gate.Release(); }
        hub.Publish("state", sim.State());
    }

    public async Task TurnOffAsync()
    {
        await gate.WaitAsync();
        try { sim.On = false; } finally { gate.Release(); }
        hub.Publish("state", sim.State());
    }

    public async Task SetScenarioAsync(string scenario)
    {
        await TurnOnAsync();
        await gate.WaitAsync();
        try { await sim.SetScenarioAsync(scenario); } finally { gate.Release(); }
        await TickAsync(); // react immediately
    }

    public async Task TickAsync()
    {
        List<Models.LiveEvent> evs;
        await gate.WaitAsync();
        try
        {
            if (!sim.On) return;
            evs = await unitOfWork.WriteAsync(async () =>
            {
                var e = await sim.TickAsync();
                // simulated readings are kept short; notifications stay for the feed
                await events.TrimReadingsAsync(options.Value.KeepReadings);
                return e;
            });
        }
        finally { gate.Release(); }
        hub.Publish("tick", new LiveTickMessage(sim.State(), evs.Where(e => e.Kind == "reading").ToList(), evs.Where(e => e.Kind == "notification").ToList()));
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(options.Value.TickSeconds), stop); } catch (TaskCanceledException) { break; }
            try { await TickAsync(); } catch (Exception e) { logger.LogError("live tick failed: {Message}", e.Message); }
        }
    }
}
