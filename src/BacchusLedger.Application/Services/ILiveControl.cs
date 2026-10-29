using BacchusLedger.Application.Contracts.Live;

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
}
