namespace BacchusLedger.Application.Contracts.Outbox;

/// GET /api/outbox/stats: messages per status, and how long the oldest pending one has waited (null: none pending).
public sealed record OutboxStatsDto(long Pending, long Processed, long Failed, double? OldestPendingAgeSeconds);
