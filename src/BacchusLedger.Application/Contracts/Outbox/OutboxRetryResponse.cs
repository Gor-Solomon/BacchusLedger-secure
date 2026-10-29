namespace BacchusLedger.Application.Contracts.Outbox;

/// POST /api/outbox/failed/{id}/retry: the message is pending again.
public sealed record OutboxRetryResponse(Guid Id, string Status);
