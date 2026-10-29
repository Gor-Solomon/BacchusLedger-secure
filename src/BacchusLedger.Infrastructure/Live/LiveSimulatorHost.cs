using BacchusLedger.Application.Contracts.Live;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Options;
using BacchusLedger.Application.Providers;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Application.Services;
using BacchusLedger.Domain.Ranges;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BacchusLedger.Infrastructure.Live;

/// Background simulator: one tick every LiveOptions.TickSeconds while Live is ON.
/// Runs outside any request: every step opens its own DI scope (and so its own DbContext).
public sealed class LiveSimulatorHost(
    IServiceScopeFactory scopes, IRangeConfigSource ranges, ILiveHub hub, IOptions<LiveOptions> options, ILogger<LiveSimulatorHost> logger) : BackgroundService, ILiveControl
{
    readonly LiveSimulator sim = new(null, new RangeConfig(ranges.Config), null);
    readonly SemaphoreSlim gate = new(1, 1);

    public LiveStateDto State() => sim.State();

    public void UseSettings(IReadOnlyDictionary<string, string> settings) => sim.UseSettings(settings);

    public async Task TurnOnAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (sim.On) return;
            await using var scope = Bind();
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
        try
        {
            await using var scope = Bind();
            await sim.SetScenarioAsync(scenario);
        }
        finally { gate.Release(); }
        await TickAsync(); // react immediately
    }

    public async Task TickAsync()
    {
        List<LiveEvent> evs;
        await gate.WaitAsync();
        try
        {
            if (!sim.On) return;
            await using var scope = Bind();
            var events = scope.ServiceProvider.GetRequiredService<ILiveEventRepository>();
            evs = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().WriteAsync(async () =>
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

    /// A new scope for one simulator step; the simulator uses its services until the scope is disposed.
    AsyncServiceScope Bind()
    {
        var scope = scopes.CreateAsyncScope();
        sim.Use(LiveSimulatorDependencies.From(scope.ServiceProvider));
        return scope;
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
