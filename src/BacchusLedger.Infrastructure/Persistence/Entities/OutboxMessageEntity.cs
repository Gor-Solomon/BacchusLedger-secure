namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table outbox_messages: an event saved in the same transaction as its data, waiting for (or done by) the outbox worker.
public sealed class OutboxMessageEntity
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string? Vessel { get; set; }

    /// Insertion order: keeps events of the same moment in the order they were written.
    public long Seq { get; set; }

    public string Status { get; set; } = "pending";
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
