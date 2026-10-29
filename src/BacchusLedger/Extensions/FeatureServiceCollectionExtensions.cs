using BacchusLedger.Services;

namespace BacchusLedger.Extensions;

/// DI registrations, one method per feature (called from Startup.ConfigureServices).
public static class FeatureServiceCollectionExtensions
{
    public static IServiceCollection AddHealthFeature(this IServiceCollection services) =>
        services.AddSingleton<IHealthService, HealthService>();

    public static IServiceCollection AddOverviewFeature(this IServiceCollection services) =>
        services.AddSingleton<IOverviewService, OverviewService>();

    public static IServiceCollection AddBatchesFeature(this IServiceCollection services) =>
        services.AddSingleton<IBatchService, BatchService>()
            .AddSingleton<IOperationService, OperationService>()
            .AddSingleton<IOriginService, OriginService>();

    public static IServiceCollection AddVesselsFeature(this IServiceCollection services) =>
        services.AddSingleton<IVesselService, VesselService>()
            .AddSingleton<ICellarService, CellarService>();

    public static IServiceCollection AddLiveFeature(this IServiceCollection services)
    {
        services.AddSingleton<LiveHub>();
        services.AddSingleton<LiveService>();
        services.AddHostedService(sp => sp.GetRequiredService<LiveService>());
        return services;
    }
}
