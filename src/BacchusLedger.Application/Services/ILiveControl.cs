using BacchusLedger.Application.Contracts.Live;
using BacchusLedger.Application.Events;
using BacchusLedger.Application.Models;

namespace BacchusLedger.Application.Services;

/// The running LIVE simulator (one tick every few seconds while it is on).
public interface ILiveControl
{
    LiveStateDto State();
    Task TurnOnAsync();
    Task TurnOffAsync();
    Task SetScenarioAsync(string scenario);
    Task TickAsync();
    void UseSettings(IReadOnlyDictionary<string, string> settings);

    /// The live alert rules on one vessel's readings of the running simulator (RangeRulesHandler). Returns the new notifications.
    IReadOnlyList<LiveEvent> ApplyRules(LiveReadingSimulated readings);
}
