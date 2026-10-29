using BacchusLedger.Application.Options;
using Microsoft.Extensions.Options;

namespace BacchusLedger.Application.Messaging;

/// Exponential backoff: after attempt n the message waits RetryBaseSeconds × 2^(n − 1) (1 s, 2 s, 4 s …, at most 1 h),
/// and after MaxAttempts failed attempts it is failed (the dead-letter).
public sealed class RetryPolicy(IOptions<OutboxOptions> options)
{
    static readonly TimeSpan Longest = TimeSpan.FromHours(1);

    public TimeSpan DelayAfter(int attempts) =>
        TimeSpan.FromSeconds(Math.Min(options.Value.RetryBaseSeconds * Math.Pow(2, Math.Max(0, attempts - 1)), Longest.TotalSeconds));

    public bool IsExhausted(int attempts) => attempts >= Math.Max(1, options.Value.MaxAttempts);
}
