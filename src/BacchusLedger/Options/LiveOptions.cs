namespace BacchusLedger.Options;

/// LIVE mode timing (configuration section "Live"; the defaults are the values the app always used).
public sealed class LiveOptions
{
    /// One simulator step every TickSeconds while Live is ON.
    public int TickSeconds { get; set; } = 3;

    /// SSE comment sent when nothing happened for this long.
    public int KeepAliveSeconds { get; set; } = 15;

    /// Simulated readings kept in live_events (notifications are always kept).
    public int KeepReadings { get; set; } = 3000;
}
