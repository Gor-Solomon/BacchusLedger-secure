namespace BacchusLedger.Application.Options;

/// Outbox worker settings (configuration section "Outbox"; OUTBOX_FAIL_RATE overrides FailRate).
public sealed class OutboxOptions
{
    /// The worker looks for pending messages this often (it also wakes up at once when a new message is committed).
    public double PollSeconds { get; set; } = 1;

    /// Messages taken per round.
    public int BatchSize { get; set; } = 50;

    /// After this many failed attempts a message becomes failed (the dead-letter).
    public int MaxAttempts { get; set; } = 5;

    /// Wait before the next attempt: RetryBaseSeconds × 2^(attempts − 1), so 1 s, 2 s, 4 s, 8 s …
    public double RetryBaseSeconds { get; set; } = 1;

    /// DEMO only: share of handler runs that fail on purpose (0 … 1), to show retries and the dead-letter. Always 0 unless set.
    public double FailRate { get; set; }

    /// Processed messages (and their processed_messages rows) are deleted after this many hours.
    public double KeepProcessedHours { get; set; } = 24;
}
