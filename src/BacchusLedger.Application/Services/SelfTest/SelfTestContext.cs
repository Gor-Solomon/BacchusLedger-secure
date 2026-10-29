using BacchusLedger.Application.Contracts.SelfTest;

namespace BacchusLedger.Application.Services.SelfTest;

/// Collects the check results of one selftest run.
public sealed class SelfTestContext(string? baseUrl, string? webRoot)
{
    public string? BaseUrl { get; } = baseUrl;
    public string? WebRoot { get; } = webRoot;
    public List<SelfTestCheckDto> Checks { get; } = [];

    public void Add(string group, string name, string expected, string actual, bool pass) => Checks.Add(new(group, name, expected, actual, pass));

    public static bool Near(double? a, double b, double tol) => a != null && Math.Abs(a.Value - b) <= tol;
}
