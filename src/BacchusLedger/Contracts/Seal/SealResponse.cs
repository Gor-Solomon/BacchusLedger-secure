namespace BacchusLedger.Contracts.Seal;

public sealed record SealResponse(
    string Batch,
    string? FinalHash,
    string? Signature,
    string? SealedAt,
    long? Entries,
    string? KeyId,
    string Algorithm,
    string VerifyUrl,
    IReadOnlyList<SealEntryDto> Chain,
    string QrPng);
