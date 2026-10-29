namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table seal_batch: The seal of a batch: final hash, signature, time and key.
public sealed class SealBatchEntity
{
    public long BatchId { get; set; }
    public string? BatchCode { get; set; }
    public string? FinalHash { get; set; }
    public string? Signature { get; set; }
    public string? SealedAt { get; set; }
    public long? Entries { get; set; }
    public string? KeyId { get; set; }
}
