using BacchusLedger.Application.Events;

namespace BacchusLedger.Application.Messaging;

/// Handles one kind of outbox event. Runs in the outbox worker, inside a transaction together with the "processed" mark,
/// at most once per message (processed_messages).
public interface IEventHandler<in TEvent> where TEvent : IOutboxEvent
{
    Task HandleAsync(TEvent e, CancellationToken ct);
}
