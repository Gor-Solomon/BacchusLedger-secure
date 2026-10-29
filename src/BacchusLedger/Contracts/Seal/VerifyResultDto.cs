namespace BacchusLedger.Contracts.Seal;

public sealed record VerifyResultDto(
    bool Ok,
    string Status,
    long? TamperedAt,
    string? TamperedWhat,
    string? FinalHash,
    string? Signature,
    string? SealedAt,
    long Entries,
    int Unsealed,
    bool SignatureOk,
    string KeyId);
