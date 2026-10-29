namespace BacchusLedger.Application.Messaging;

/// The handlers run in this process, right away (no broker).
public sealed class InProcessMessagePublisher(IEventDispatcher dispatcher) : IMessagePublisher
{
    public Task PublishAsync(OutboxMessage message, CancellationToken ct) => dispatcher.DispatchAsync(message, ct);
}
