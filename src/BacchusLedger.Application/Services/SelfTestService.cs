using BacchusLedger.Application.Contracts.SelfTest;
using BacchusLedger.Application.Services.SelfTest;

namespace BacchusLedger.Application.Services;

public sealed class SelfTestService(IEnumerable<ISelfTestSection> sections) : ISelfTestService
{
    public async Task<SelfTestResponse> RunAsync(string localBaseUrl, string? webRootPath)
    {
        var ctx = new SelfTestContext(localBaseUrl, webRootPath);
        foreach (var s in sections) await s.RunAsync(ctx);
        var items = ctx.Checks;
        return new(items.All(i => i.Pass), items.Count(i => i.Pass), items.Count,
            "Loss checks use the dataset rows and the default DEMO ASSUMPTION norms. Reading and live probes run in a rolled-back transaction. Assistant checks never call an AI provider.",
            items);
    }
}
