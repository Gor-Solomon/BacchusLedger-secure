namespace BacchusLedger.Application.Models;

/// Messages per status in outbox_messages, and when the oldest pending one happened (null: none pending).
public sealed record OutboxCounts(long Pending, long Processed, long Failed, DateTime? OldestPendingAt);
