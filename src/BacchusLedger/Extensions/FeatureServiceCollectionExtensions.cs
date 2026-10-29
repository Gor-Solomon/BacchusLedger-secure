using BacchusLedger.Services;

namespace BacchusLedger.Extensions;

/// DI registrations, one method per feature (called from Startup.ConfigureServices).
public static class FeatureServiceCollectionExtensions
{
    public static IServiceCollection AddHealthFeature(this IServiceCollection services) =>
        services.AddSingleton<IHealthService, HealthService>();

    public static IServiceCollection AddOverviewFeature(this IServiceCollection services) =>
        services.AddSingleton<IOverviewService, OverviewService>();

    public static IServiceCollection AddLiveFeature(this IServiceCollection services)
    {
        services.AddSingleton<LiveHub>();
        services.AddSingleton<LiveService>();
        services.AddHostedService(sp => sp.GetRequiredService<LiveService>());
        return services;
    }
}
