namespace BacchusLedger.Application.Contracts.Outbox;

/// A message that gave up after MaxAttempts (the dead-letter), with the last error.
public sealed record FailedMessageDto(Guid Id, string Type, string? Vessel, DateTime OccurredAt, int Attempts, string? LastError);
