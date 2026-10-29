namespace BacchusLedger.Application.Contracts.Seal;

public sealed record VerifyResponse(string? Batch, VerifyResultDto Result, string PublicKey, string KeyNote);
