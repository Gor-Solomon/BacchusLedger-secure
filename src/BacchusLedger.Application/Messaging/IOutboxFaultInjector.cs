namespace BacchusLedger.Application.Messaging;

/// DEMO: makes handlers fail on purpose (OUTBOX_FAIL_RATE) so retries and the dead-letter can be shown. Off by default.
public interface IOutboxFaultInjector
{
    /// Throws before the handler runs (so a failed attempt leaves nothing half done) for a share of the runs.
    void BeforeHandle(string handler);
}
