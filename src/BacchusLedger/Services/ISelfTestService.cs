using BacchusLedger.Contracts.SelfTest;

namespace BacchusLedger.Services;

/// Acceptance checks (calibration, losses, readings, live, assistant, ranges, ledger, seal), run on the dataset rows.
public interface ISelfTestService
{
    /// localBaseUrl: this server (for the HTTP checks); webRootPath: the served files (for the key leak check).
    Task<SelfTestResponse> RunAsync(string localBaseUrl, string? webRootPath);
}
