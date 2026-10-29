using BacchusLedger.Providers;
using BacchusLedger.Repositories;
using BacchusLedger.Services;
using BacchusLedger.Domain.Ranges;

namespace BacchusLedger.Infrastructure.Live;

public sealed class LiveSimulatorFactory(
    IBatchRepository batches, IVesselRepository vessels, IReadingRepository readings, IOperationRepository operations, ILiveEventRepository events,
    ISettingsRepository settings, ILossAnalysisService losses, IRangeConfigSource ranges) : ILiveSimulatorFactory
{
    public ILiveSimulator Create(int? seed = null) =>
        new LiveSimulator(batches, vessels, readings, operations, events, settings, losses, new RangeConfig(ranges.Config), seed);
}
