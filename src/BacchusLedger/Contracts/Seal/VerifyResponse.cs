namespace BacchusLedger.Contracts.Seal;

public sealed record VerifyResponse(string? Batch, VerifyResultDto Result, string PublicKey, string KeyNote);
