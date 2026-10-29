using BacchusLedger.Contracts.SelfTest;

namespace BacchusLedger.Services;

public sealed class SelfTestService : ISelfTestService
{
    public Task<SelfTestResponse> RunAsync(string localBaseUrl, string? webRootPath) => Task.FromResult(SelfTest.Run(localBaseUrl, webRootPath));
}
