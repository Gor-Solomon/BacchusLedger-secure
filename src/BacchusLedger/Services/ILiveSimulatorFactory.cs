namespace BacchusLedger.Services;

public interface ILiveSimulatorFactory
{
    /// seed: a fixed random seed (selftest) or null.
    ILiveSimulator Create(int? seed = null);
}
