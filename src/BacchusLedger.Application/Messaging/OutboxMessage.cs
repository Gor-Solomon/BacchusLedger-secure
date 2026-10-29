namespace BacchusLedger.Application.Messaging;

/// One row of outbox_messages as the worker sees it. Payload is the event as JSON (OutboxEventTypes).
public sealed record OutboxMessage(Guid Id, string Type, string Payload, DateTime OccurredAt, string? Vessel, int Attempts, DateTime NextAttemptAt);
