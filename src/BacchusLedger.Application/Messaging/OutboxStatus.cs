namespace BacchusLedger.Application.Messaging;

/// Values of outbox_messages.status.
public static class OutboxStatus
{
    public const string Pending = "pending";
    public const string Processed = "processed";

    /// Gave up after OutboxOptions.MaxAttempts (the dead-letter); POST /api/outbox/failed/{id}/retry puts it back to pending.
    public const string Failed = "failed";
}
