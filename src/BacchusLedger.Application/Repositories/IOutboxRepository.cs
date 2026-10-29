using BacchusLedger.Application.Contracts.Outbox;
using BacchusLedger.Application.Messaging;
using BacchusLedger.Application.Models;

namespace BacchusLedger.Application.Repositories;

/// outbox_messages and processed_messages (the outbox worker's bookkeeping).
public interface IOutboxRepository
{
    /// Pending messages, due or not, oldest first (occurred_at, then the order they were written).
    Task<List<OutboxMessage>> ListPendingAsync(int limit);

    Task MarkProcessedAsync(Guid id, DateTime at);

    /// A failed attempt: pending again from nextAttemptAt, or failed (the dead-letter) when deadLetter is true.
    Task MarkAttemptFailedAsync(Guid id, int attempts, DateTime nextAttemptAt, string error, bool deadLetter);

    /// processed_messages: has this handler already handled this message?
    Task<bool> HasHandledAsync(Guid messageId, string handler);

    Task MarkHandledAsync(Guid messageId, string handler, DateTime at);

    Task<OutboxCounts> CountAsync();

    /// Failed messages, the most recently tried first.
    Task<List<FailedMessageDto>> ListFailedAsync(int limit);

    /// failed → pending, attempts start again at 0. False when there is no failed message with this id.
    Task<bool> RetryFailedAsync(Guid id, DateTime now);

    /// Deletes processed messages older than cutoff, and the processed_messages rows of messages that are gone.
    Task<int> DeleteProcessedBeforeAsync(DateTime cutoff);
}
