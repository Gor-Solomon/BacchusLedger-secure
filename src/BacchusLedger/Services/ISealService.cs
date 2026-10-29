using BacchusLedger.Contracts.Seal;

namespace BacchusLedger.Services;

/// Tamper evidence: every record entry of a batch (deliveries, operations, lab, readings) is chained,
/// hash = SHA-256(previous hash | table | row content), and the final hash is signed with a DEMO ECDSA P-256 key.
public interface ISealService
{
    /// Seals new entries of the batch and returns the chain, signature and a QR code for the verify page under baseUrl.
    Task<ServiceResult<SealResponse>> SealAsync(string? batch, string baseUrl);

    Task<ServiceResult<VerifyResponse>> VerifyAsync(string? batch);

    /// DEMO ONLY: undo the demo tamper (and seal again if needed).
    Task<ServiceResult<ResetSealResponse>> ResetDemoAsync(string? batch);

    /// DEMO ONLY: change one sealed lab value so verify shows the failure.
    Task<ServiceResult<TamperResponse>> TamperDemoAsync(string? batch);

    /// On start: seal every batch (and rebuild all chains once when the chain format changed).
    Task SealAllAsync();

    /// Adds new entries to the end of the batch's chain and signs the final hash (inside the running transaction).
    Task EnsureAsync(long batchId);

    /// Removes the batch's chain, seal and tamper log (demo replay reset).
    Task DropAsync(long batchId);

    /// Recomputes the chain from the rows as they are now and compares it with the sealed hashes and the signature.
    Task<VerifyResultDto> CheckAsync(long batchId);

    /// DEMO ONLY: change one sealed lab temperature of the batch (inside the running transaction); null when there is none.
    Task<TamperResponse?> TamperAsync(long batchId);

    bool CheckSignature(string finalHash, string signature);
}
