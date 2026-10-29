using BacchusLedger.Application.Contracts.Live;
using BacchusLedger.Application.Models;

namespace BacchusLedger.Application.Services;

/// The LIVE mode simulator. Everything it produces is SIMULATED and stored only in live_events.
public interface ILiveSimulator
{
    bool On { get; set; }

    /// Loads the demo vessels; base values start from their last recorded values (settings: null = the stored settings).
    Task StartAsync(IReadOnlyDictionary<string, string>? settings = null);

    Task SetScenarioAsync(string scenario);

    /// One simulator step; its events are written to live_events (in the running transaction, if any).
    Task<List<LiveEvent>> TickAsync();

    /// Settings changed (thresholds are editable): used from the next tick.
    void UseSettings(IReadOnlyDictionary<string, string> settings);

    /// Allowed maximum of a measurement on a streaming vessel (config/ranges.json), if any.
    double? AllowedMax(string vesselCode, string metric);

    LiveStateDto State();
}
