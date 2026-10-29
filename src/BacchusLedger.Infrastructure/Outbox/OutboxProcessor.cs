using BacchusLedger.Application.Messaging;
using BacchusLedger.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BacchusLedger.Infrastructure.Outbox;

/// The outbox worker: every OutboxOptions.PollSeconds (or at once after a commit with new messages) it publishes the due
/// pending messages, in a new DI scope per round. One worker only: SQLite has one writer, and order per vessel is kept here.
/// Messages left pending by a crash or a restart are simply still pending, so they are processed after the next start.
public sealed class OutboxProcessor(
    IServiceScopeFactory scopes, OutboxSignal signal, IOptions<OutboxOptions> options, TimeProvider clock, ILogger<OutboxProcessor> logger) : BackgroundService
{
    static readonly TimeSpan CleanupEvery = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        await Task.Yield(); // do not hold up the app start
        if (options.Value.FailRate > 0)
            logger.LogWarning("outbox: OUTBOX_FAIL_RATE={Rate}: handlers fail on purpose (demo of retries and the dead-letter)", options.Value.FailRate);
        var nextCleanup = clock.GetUtcNow();
        while (!stop.IsCancellationRequested)
        {
            var tried = 0;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var batch = scope.ServiceProvider.GetRequiredService<IOutboxBatchProcessor>();
                tried = await batch.ProcessDueAsync(stop);
                if (clock.GetUtcNow() >= nextCleanup)
                {
                    await batch.CleanupAsync();
                    nextCleanup = clock.GetUtcNow() + CleanupEvery;
                }
            }
            catch (Exception e) when (!stop.IsCancellationRequested)
            {
                logger.LogWarning("outbox: round failed, trying again: {Message}", e.Message);
            }
            if (tried > 0) continue; // more may be due right away
            try { await signal.WaitAsync(TimeSpan.FromSeconds(Math.Max(0.01, options.Value.PollSeconds)), stop); }
            catch (OperationCanceledException) { break; }
        }
    }
}
