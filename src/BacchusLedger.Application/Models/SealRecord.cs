namespace BacchusLedger.Application.Models;

/// The seal of a batch: final hash of the chain and its signature.
public sealed record SealRecord(long BatchId, string BatchCode, string FinalHash, string Signature, string? SealedAt, long? Entries, string? KeyId);
