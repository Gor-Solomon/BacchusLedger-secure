using BacchusLedger.Application.Options;
using BacchusLedger.Application.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BacchusLedger.Application.Messaging;

public sealed class OutboxBatchProcessor(
    IOutboxRepository outbox, IMessagePublisher publisher, IUnitOfWork unitOfWork, RetryPolicy retry, TimeProvider clock, IOptions<OutboxOptions> options,
    ILogger<OutboxBatchProcessor> logger) : IOutboxBatchProcessor
{
    const int MaxErrorLength = 1000;

    DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<int> ProcessDueAsync(CancellationToken stop)
    {
        var size = Math.Max(1, options.Value.BatchSize);
        var pending = await outbox.ListPendingAsync(size * 10);
        // A vessel whose oldest pending message waits for a retry is blocked: its later messages wait too (order per vessel).
        var blocked = new HashSet<string>();
        var tried = 0;
        foreach (var m in pending)
        {
            if (tried >= size || stop.IsCancellationRequested) break; // shutdown: the current message was finished, the rest stays pending
            if (m.Vessel != null && blocked.Contains(m.Vessel)) continue;
            if (m.NextAttemptAt > Now)
            {
                if (m.Vessel != null) blocked.Add(m.Vessel);
                continue;
            }
            if (!await DeliverAsync(m) && m.Vessel != null) blocked.Add(m.Vessel);
            tried++;
        }
        return tried;
    }

    public Task<int> CleanupAsync() =>
        unitOfWork.WriteAsync(() => outbox.DeleteProcessedBeforeAsync(Now - TimeSpan.FromHours(options.Value.KeepProcessedHours)));

    /// A message is never cancelled half way: it is published and marked processed, or its failure is recorded.
    async Task<bool> DeliverAsync(OutboxMessage m)
    {
        try
        {
            await publisher.PublishAsync(m, CancellationToken.None);
            await unitOfWork.WriteAsync(async () => { await outbox.MarkProcessedAsync(m.Id, Now); return 0; });
            return true;
        }
        catch (Exception e)
        {
            var attempts = m.Attempts + 1;
            var dead = retry.IsExhausted(attempts);
            var error = e.Message.Length > MaxErrorLength ? e.Message[..MaxErrorLength] : e.Message;
            await unitOfWork.WriteAsync(async () =>
            {
                await outbox.MarkAttemptFailedAsync(m.Id, attempts, Now + retry.DelayAfter(attempts), error, dead);
                return 0;
            });
            if (dead)
                logger.LogError("outbox: {Type} {Id} failed {Attempts} times and was moved to failed (dead-letter): {Error}", m.Type, m.Id, attempts, error);
            else
                logger.LogWarning("outbox: {Type} {Id} attempt {Attempts} failed, next try in {Delay} s: {Error}",
                    m.Type, m.Id, attempts, retry.DelayAfter(attempts).TotalSeconds, error);
            return false;
        }
    }
}
