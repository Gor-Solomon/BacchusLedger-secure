using BacchusLedger.Application.Providers;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Application.Services;
using BacchusLedger.Application.Services.Assistant;
using BacchusLedger.Infrastructure.Ai;
using BacchusLedger.Infrastructure.Live;
using BacchusLedger.Infrastructure.Persistence;
using BacchusLedger.Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace BacchusLedger.Infrastructure;

/// SQLite persistence, file and HTTP providers, AI providers and the background live simulator.
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services) => services
        .AddPersistence()
        .AddProviders()
        .AddAiProviders()
        .AddLiveSimulator();

    public static IServiceCollection AddPersistence(this IServiceCollection services) => services
        .AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>()
        .AddSingleton<IUnitOfWork, SqliteUnitOfWork>()
        .AddSingleton<IWineryRepository, SqliteWineryRepository>()
        .AddSingleton<IBatchRepository, SqliteBatchRepository>()
        .AddSingleton<IVesselRepository, SqliteVesselRepository>()
        .AddSingleton<IOperationRepository, SqliteOperationRepository>()
        .AddSingleton<IOriginRepository, SqliteOriginRepository>()
        .AddSingleton<ILabRepository, SqliteLabRepository>()
        .AddSingleton<IReadingRepository, SqliteReadingRepository>()
        .AddSingleton<ISettingsRepository, SqliteSettingsRepository>()
        .AddSingleton<ILiveEventRepository, SqliteLiveEventRepository>()
        .AddSingleton<IDemoRepository, SqliteDemoRepository>()
        .AddSingleton<IMaintenanceRepository, SqliteMaintenanceRepository>()
        .AddSingleton<ISealRepository, SqliteSealRepository>()
        .AddSingleton<IJournalRepository, SqliteJournalRepository>()
        .AddSingleton<IDiagnosticsRepository, SqliteDiagnosticsRepository>()
        .AddSingleton<DatabaseSeeder>()
        .AddSingleton<IDatabaseInitializer, DatabaseInitializer>();

    public static IServiceCollection AddProviders(this IServiceCollection services)
    {
        services.AddSingleton<ISigningKeyProvider, PemSigningKeyProvider>();
        services.AddSingleton<IRangeConfigSource, FileRangeConfigSource>();
        services.AddSingleton<IFactsSource, FileFactsSource>();
        services.AddSingleton<IQrCodeRenderer, QrCoderRenderer>();
        services.AddSingleton<ILocalApiClient, LocalApiClient>();
        services.AddHttpClient("local", c => c.Timeout = TimeSpan.FromSeconds(10));
        return services;
    }

    public static IServiceCollection AddAiProviders(this IServiceCollection services)
    {
        services.AddHttpClient("ai", c => c.Timeout = Timeout.InfiniteTimeSpan); // the per-question time limit is a CancellationToken
        services.AddSingleton<IAiProvider, OpenAiProvider>();
        services.AddSingleton<IAiProvider, GeminiProvider>();
        return services;
    }

    public static IServiceCollection AddLiveSimulator(this IServiceCollection services)
    {
        services.AddSingleton<ILiveHub, LiveHub>();
        services.AddSingleton<ILiveSimulatorFactory, LiveSimulatorFactory>();
        services.AddSingleton<LiveSimulatorHost>();
        services.AddSingleton<ILiveControl>(sp => sp.GetRequiredService<LiveSimulatorHost>());
        services.AddHostedService(sp => sp.GetRequiredService<LiveSimulatorHost>());
        return services;
    }
}
