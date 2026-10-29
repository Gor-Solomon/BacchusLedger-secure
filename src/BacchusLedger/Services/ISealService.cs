using BacchusLedger.Contracts.Seal;

namespace BacchusLedger.Services;

/// Tamper evidence: hash chain over a batch's record entries + DEMO ECDSA signature.
public interface ISealService
{
    /// Seals new entries of the batch and returns the chain, signature and a QR code for the verify page under baseUrl.
    Task<ServiceResult<SealResponse>> SealAsync(string? batch, string baseUrl);

    Task<ServiceResult<VerifyResponse>> VerifyAsync(string? batch);

    /// DEMO ONLY: undo the demo tamper (and seal again if needed).
    Task<ServiceResult<ResetSealResponse>> ResetDemoAsync(string? batch);

    /// DEMO ONLY: change one sealed lab value so verify shows the failure.
    Task<ServiceResult<TamperResponse>> TamperDemoAsync(string? batch);
}
