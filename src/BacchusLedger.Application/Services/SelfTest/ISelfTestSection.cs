namespace BacchusLedger.Application.Services.SelfTest;

/// One group of selftest checks. Sections run in registration order.
public interface ISelfTestSection
{
    Task RunAsync(SelfTestContext ctx);
}
