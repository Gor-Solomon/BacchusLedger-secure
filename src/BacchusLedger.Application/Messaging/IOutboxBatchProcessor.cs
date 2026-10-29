namespace BacchusLedger.Application.Messaging;

/// One round of the outbox worker (the background OutboxProcessor calls it in a new DI scope each time).
public interface IOutboxBatchProcessor
{
    /// Publishes the due pending messages, oldest first, keeping the order per vessel; failures are retried with backoff.
    /// When stop is signalled, the current message is finished and the rest stays pending. Returns the number of messages tried.
    Task<int> ProcessDueAsync(CancellationToken stop);

    /// Deletes processed messages older than OutboxOptions.KeepProcessedHours.
    Task<int> CleanupAsync();
}
