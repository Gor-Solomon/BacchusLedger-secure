namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table processed_messages: handler X has handled message Y (so it never runs twice for the same message).
public sealed class ProcessedMessageEntity
{
    public Guid MessageId { get; set; }
    public string Handler { get; set; } = "";
    public DateTime ProcessedAt { get; set; }
}
