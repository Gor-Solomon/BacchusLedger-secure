using BacchusLedger.Application.Contracts.Live;
using BacchusLedger.Application.Events;
using BacchusLedger.Application.Models;

namespace BacchusLedger.Application.Services;

/// The LIVE mode simulator. Everything it produces is SIMULATED and stored only in live_events.
public interface ILiveSimulator
{
    bool On { get; set; }

    /// Loads the demo vessels; base values start from their last recorded values (settings: null = the stored settings).
    Task StartAsync(IReadOnlyDictionary<string, string>? settings = null);

    Task SetScenarioAsync(string scenario);

    /// One simulator step without the rules: the readings (and notifications waiting from a scenario change) are written to
    /// live_events (in the running transaction, if any). The rules run later on each vessel's LiveReadingSimulated (ApplyRules).
    Task<SimulatedTick> ProduceAsync();

    /// The live alert rules on one vessel's readings (in memory). Returns the new notifications, not yet stored.
    IReadOnlyList<LiveEvent> ApplyRules(LiveReadingSimulated readings);

    /// ProduceAsync and ApplyRules at once, notifications stored too (the selftest's simulator, inside its rolled-back probe).
    Task<List<LiveEvent>> TickAsync();

    /// Settings changed (thresholds are editable): used from the next tick.
    void UseSettings(IReadOnlyDictionary<string, string> settings);

    /// Allowed maximum of a measurement on a streaming vessel (config/ranges.json), if any.
    double? AllowedMax(string vesselCode, string metric);

    LiveStateDto State();
}
