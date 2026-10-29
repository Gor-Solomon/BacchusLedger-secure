using System.Globalization;
using BacchusLedger.Application.Options;
using Microsoft.Extensions.Options;

namespace BacchusLedger.Application.Messaging;

public sealed class OutboxFaultInjector(IOptions<OutboxOptions> options) : IOutboxFaultInjector
{
    public void BeforeHandle(string handler)
    {
        var rate = options.Value.FailRate;
        if (rate > 0 && Random.Shared.NextDouble() < rate)
            throw new InvalidOperationException($"simulated failure (OUTBOX_FAIL_RATE={rate.ToString(CultureInfo.InvariantCulture)}, demo)");
    }
}
