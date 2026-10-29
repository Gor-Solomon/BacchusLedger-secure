using BacchusLedger.Application.Contracts.Live;
using BacchusLedger.Application.Events;
using BacchusLedger.Application.Messaging;
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

    public IReadOnlyList<LiveEvent> ApplyRules(LiveReadingSimulated readings) => sim.ApplyRules(readings);

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

    /// The readings and their outbox events are saved in one transaction; the outbox worker then runs the rules
    /// (RangeRulesHandler) and pushes the "tick" messages to the live stream (LiveStreamHandler).
    public async Task TickAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (!sim.On) return;
            await using var scope = Bind();
            var events = scope.ServiceProvider.GetRequiredService<ILiveEventRepository>();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().WriteAsync(async () =>
            {
                var tick = await sim.ProduceAsync();
                foreach (var note in tick.Notes) await outbox.EnqueueAsync(RangeAlertRaised.For(note, DateTime.UtcNow));
                foreach (var readings in tick.Readings) await outbox.EnqueueAsync(readings);
                // simulated readings are kept short; notifications stay for the feed
                await events.TrimReadingsAsync(options.Value.KeepReadings);
                return 0;
            });
        }
        finally { gate.Release(); }
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
