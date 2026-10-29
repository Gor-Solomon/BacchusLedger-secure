using BacchusLedger.Application.Repositories;
using BacchusLedger.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BacchusLedger.Infrastructure.Live;

/// The scoped services the simulator reads and writes through (one DbContext scope).
public sealed record LiveSimulatorDependencies(
    IBatchRepository Batches, IVesselRepository Vessels, IReadingRepository Readings, IOperationRepository Operations, ILiveEventRepository Events,
    ISettingsRepository Settings, ILossAnalysisService Losses)
{
    public static LiveSimulatorDependencies From(IServiceProvider sp) => new(
        sp.GetRequiredService<IBatchRepository>(), sp.GetRequiredService<IVesselRepository>(), sp.GetRequiredService<IReadingRepository>(),
        sp.GetRequiredService<IOperationRepository>(), sp.GetRequiredService<ILiveEventRepository>(), sp.GetRequiredService<ISettingsRepository>(),
        sp.GetRequiredService<ILossAnalysisService>());
}
