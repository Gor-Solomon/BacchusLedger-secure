namespace BacchusLedger.Application.Messaging;

/// Delivers an outbox message to its consumers. Today in the same process (InProcessMessagePublisher);
/// a message broker (Kafka, Azure Service Bus, RabbitMQ) can replace it without changing business code.
public interface IMessagePublisher
{
    /// Returns when the message is delivered; throws when it is not (the outbox worker then retries it).
    Task PublishAsync(OutboxMessage message, CancellationToken ct);
}
