namespace BacchusLedger.Infrastructure.Outbox;

/// Wakes the outbox worker as soon as a transaction with new outbox messages is committed (it also polls on its own).
public sealed class OutboxSignal
{
    readonly SemaphoreSlim signal = new(0, 1);

    public void Notify()
    {
        try { signal.Release(); } catch (SemaphoreFullException) { } // already signalled
    }

    /// True when woken up, false when the timeout passed.
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => signal.WaitAsync(timeout, ct);
}
