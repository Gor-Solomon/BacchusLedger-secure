namespace BacchusLedger.Application.Messaging;

/// Runs every IEventHandler of a message's event type (found in DI).
public interface IEventDispatcher
{
    /// Each handler runs in its own transaction together with its processed_messages row, at most once per message.
    /// Throws EventHandlingException when a handler failed; the other handlers still run.
    Task DispatchAsync(OutboxMessage message, CancellationToken ct);
}
