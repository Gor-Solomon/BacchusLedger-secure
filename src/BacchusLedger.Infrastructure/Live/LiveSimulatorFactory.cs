using BacchusLedger.Application.Providers;
using BacchusLedger.Application.Services;
using BacchusLedger.Domain.Ranges;

namespace BacchusLedger.Infrastructure.Live;

/// Simulators bound to the current scope (the selftest runs them inside its rolled-back transaction).
public sealed class LiveSimulatorFactory(IServiceProvider scope, IRangeConfigSource ranges) : ILiveSimulatorFactory
{
    public ILiveSimulator Create(int? seed = null) => new LiveSimulator(LiveSimulatorDependencies.From(scope), new RangeConfig(ranges.Config), seed);
}
