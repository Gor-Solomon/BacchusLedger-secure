using BacchusLedger.Application.Events;
using BacchusLedger.Application.Messaging;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Options;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BacchusLedger.Infrastructure.Tests.Outbox;

/// The outbox on a real SQLite file: dispatcher routing, backoff, dead-letter, idempotency, order per vessel.
/// The app's own handlers are replaced by recording test handlers; the clock is set by hand.
public sealed class OutboxTests : IAsyncLifetime, IDisposable
{
    readonly ManualClock clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    readonly HandlerLog log = new();
    readonly TestDatabase t;

    public OutboxTests()
    {
        t = new TestDatabase(s =>
        {
            s.RemoveAll<IEventHandler<SensorReadingReceived>>();
            s.RemoveAll<IEventHandler<LiveReadingSimulated>>();
            s.RemoveAll<IEventHandler<RangeAlertRaised>>();
            s.AddSingleton<TimeProvider>(clock);
            s.AddSingleton(log);
            s.AddScoped<IEventHandler<SensorReadingReceived>, SensorHandler>();
            s.AddScoped<IEventHandler<RangeAlertRaised>, AlertHandler>();
            s.AddScoped<IEventHandler<RangeAlertRaised>, FlakyAlertHandler>();
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = t.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<BacchusLedgerDbContext>().Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => t.Dispose();

    DateTime Now => clock.GetUtcNow().UtcDateTime;

    static LiveEvent Event(string vessel) => new() { Kind = "notification", VesselCode = vessel, Level = "red", Rule = "test", Message = "test", Source = "SIMULATED" };

    async Task Enqueue(params IOutboxEvent[] events)
    {
        using var scope = t.Services.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().WriteAsync(async () => { foreach (var e in events) await outbox.EnqueueAsync(e); return 0; });
    }

    async Task<int> Round()
    {
        using var scope = t.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IOutboxBatchProcessor>().ProcessDueAsync(CancellationToken.None);
    }

    async Task<T> Db<T>(Func<BacchusLedgerDbContext, Task<T>> query)
    {
        using var scope = t.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<BacchusLedgerDbContext>());
    }

    Task<Persistence.Entities.OutboxMessageEntity> Row(Guid id) => Db(db => db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id));

    [Fact]
    public async Task Dispatcher_runs_only_the_handlers_of_the_event_type()
    {
        var e = SensorReadingReceived.For(Event("F-2"), Now);
        await Enqueue(e);
        Assert.Equal(1, await Round());

        Assert.Equal([$"SensorHandler:{e.EventId}"], log.Calls);
        Assert.Equal(OutboxStatus.Processed, (await Row(e.EventId)).Status);
    }

    [Fact]
    public async Task Same_message_dispatched_twice_runs_each_handler_once()
    {
        var e = SensorReadingReceived.For(Event("F-2"), Now);
        await Enqueue(e);
        using var scope = t.Services.CreateScope();
        var types = scope.ServiceProvider.GetRequiredService<OutboxEventTypes>();
        var message = new OutboxMessage(e.EventId, types.NameOf(e), types.Serialize(e), e.OccurredAt, e.Vessel, 0, e.OccurredAt);
        var dispatcher = scope.ServiceProvider.GetRequiredService<IEventDispatcher>();

        await dispatcher.DispatchAsync(message, CancellationToken.None);
        await dispatcher.DispatchAsync(message, CancellationToken.None);

        Assert.Single(log.Calls);
        Assert.Equal(1, await Db(db => db.ProcessedMessages.CountAsync(p => p.MessageId == e.EventId && p.Handler == "SensorHandler")));
    }

    [Fact]
    public void Backoff_doubles_from_one_second_and_gives_up_after_max_attempts()
    {
        var retry = new RetryPolicy(Microsoft.Extensions.Options.Options.Create(new OutboxOptions()));
        Assert.Equal([1, 2, 4, 8, 16], Enumerable.Range(1, 5).Select(n => retry.DelayAfter(n).TotalSeconds));
        Assert.False(retry.IsExhausted(4));
        Assert.True(retry.IsExhausted(5));
    }

    [Fact]
    public async Task A_failing_handler_is_retried_with_backoff_then_moved_to_failed()
    {
        log.FailAlways = true;
        var e = RangeAlertRaised.For(Event("Q-3"), Now);
        await Enqueue(e);
        var started = Now;
        var waits = new List<double>();
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            Assert.Equal(1, await Round());
            var row = await Row(e.EventId);
            Assert.Equal(attempt, row.Attempts);
            Assert.Contains("FlakyAlertHandler: always fails", row.LastError);
            if (attempt < 5)
            {
                Assert.Equal(OutboxStatus.Pending, row.Status);
                waits.Add((row.NextAttemptAt - Now).TotalSeconds);
                Assert.Equal(0, await Round()); // not due yet
                clock.Advance(row.NextAttemptAt - Now);
            }
            else Assert.Equal(OutboxStatus.Failed, row.Status);
        }
        Assert.Equal([1, 2, 4, 8], waits);
        Assert.Equal(15, (Now - started).TotalSeconds);
        Assert.Equal(0, await Round()); // the dead-letter is not tried again
        // the handler that succeeded ran once, not five times
        Assert.Single(log.Calls, c => c.StartsWith("AlertHandler"));

        // retry: pending again, and this time it goes through
        log.FailAlways = false;
        using (var scope = t.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().WriteAsync(() => scope.ServiceProvider.GetRequiredService<IOutboxRepository>().RetryFailedAsync(e.EventId, Now));
        Assert.Equal(1, await Round());
        Assert.Equal(OutboxStatus.Processed, (await Row(e.EventId)).Status);
        Assert.Single(log.Calls, c => c.StartsWith("AlertHandler"));
    }

    [Fact]
    public async Task Later_messages_of_a_vessel_wait_for_its_retrying_message_other_vessels_do_not()
    {
        log.FailOnce.Add("Q-3");
        var first = RangeAlertRaised.For(Event("Q-3"), Now);
        var second = RangeAlertRaised.For(Event("Q-3"), Now);
        var other = RangeAlertRaised.For(Event("F-2"), Now);
        await Enqueue(first, second, other);

        Assert.Equal(2, await Round()); // first (fails), other (ok); second waits behind first
        Assert.Equal(OutboxStatus.Pending, (await Row(second.EventId)).Status);
        Assert.Equal(0, (await Row(second.EventId)).Attempts);
        Assert.Equal(OutboxStatus.Processed, (await Row(other.EventId)).Status);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, await Round());
        Assert.Equal(OutboxStatus.Processed, (await Row(first.EventId)).Status);
        Assert.Equal(OutboxStatus.Processed, (await Row(second.EventId)).Status);
        var flaky = log.Calls.Where(c => c.StartsWith("FlakyAlertHandler")).ToList();
        Assert.Equal([$"FlakyAlertHandler:{other.EventId}", $"FlakyAlertHandler:{first.EventId}", $"FlakyAlertHandler:{second.EventId}"], flaky);
    }

    [Fact]
    public async Task Enqueue_outside_a_transaction_is_refused()
    {
        using var scope = t.Services.CreateScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IOutbox>().EnqueueAsync(SensorReadingReceived.For(Event("F-2"), Now)));
    }

    public sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        DateTimeOffset now = start;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }

    /// What the test handlers did ("Handler:message id", successful runs only).
    public sealed class HandlerLog
    {
        public List<string> Calls { get; } = [];
        public bool FailAlways { get; set; }
        public HashSet<string> FailOnce { get; } = [];
    }

    sealed class SensorHandler(HandlerLog log) : IEventHandler<SensorReadingReceived>
    {
        public Task HandleAsync(SensorReadingReceived e, CancellationToken ct) { log.Calls.Add($"SensorHandler:{e.EventId}"); return Task.CompletedTask; }
    }

    sealed class AlertHandler(HandlerLog log) : IEventHandler<RangeAlertRaised>
    {
        public Task HandleAsync(RangeAlertRaised e, CancellationToken ct) { log.Calls.Add($"AlertHandler:{e.EventId}"); return Task.CompletedTask; }
    }

    sealed class FlakyAlertHandler(HandlerLog log) : IEventHandler<RangeAlertRaised>
    {
        public Task HandleAsync(RangeAlertRaised e, CancellationToken ct)
        {
            if (log.FailAlways) throw new InvalidOperationException("always fails");
            if (log.FailOnce.Remove(e.Vessel!)) throw new InvalidOperationException("fails once");
            log.Calls.Add($"FlakyAlertHandler:{e.EventId}");
            return Task.CompletedTask;
        }
    }
}
