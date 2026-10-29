using BacchusLedger.Application.Providers;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Application.Services;
using BacchusLedger.Application.Services.Assistant;
using BacchusLedger.Infrastructure.Ai;
using BacchusLedger.Infrastructure.Live;
using BacchusLedger.Infrastructure.Persistence;
using BacchusLedger.Infrastructure.Providers;
using BacchusLedger.Application.Options;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        .AddDbContext<BacchusLedgerDbContext>((sp, o) => o.UseSqlite(ConnectionString(sp.GetRequiredService<IOptions<StorageOptions>>().Value.DbPath)))
        .AddSingleton<SqliteWriteGate>()
        .AddScoped<ISqliteConnectionFactory, SqliteConnectionFactory>()
        .AddScoped<IUnitOfWork, EfUnitOfWork>()
        .AddScoped<IWineryRepository, EfWineryRepository>()
        .AddScoped<IBatchRepository, EfBatchRepository>()
        .AddScoped<IVesselRepository, EfVesselRepository>()
        .AddScoped<IOperationRepository, EfOperationRepository>()
        .AddScoped<IOriginRepository, SqliteOriginRepository>()
        .AddScoped<ILabRepository, EfLabRepository>()
        .AddScoped<IReadingRepository, EfReadingRepository>()
        .AddScoped<ISettingsRepository, EfSettingsRepository>()
        .AddScoped<ILiveEventRepository, EfLiveEventRepository>()
        .AddScoped<IDemoRepository, SqliteDemoRepository>()
        .AddScoped<IMaintenanceRepository, SqliteMaintenanceRepository>()
        .AddScoped<ISealRepository, SqliteSealRepository>()
        .AddScoped<IJournalRepository, SqliteJournalRepository>()
        .AddScoped<IDiagnosticsRepository, SqliteDiagnosticsRepository>()
        .AddScoped<DatabaseSeeder>()
        .AddScoped<IDatabaseInitializer, DatabaseInitializer>();

    /// Same SQLite settings as before: foreign keys on, wait up to 5 s for a lock (was PRAGMA busy_timeout=5000), default journal mode.
    static string ConnectionString(string dbPath) =>
        new SqliteConnectionStringBuilder { DataSource = dbPath, ForeignKeys = true, DefaultTimeout = 5 }.ToString();

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
        services.AddScoped<ILiveSimulatorFactory, LiveSimulatorFactory>();
        services.AddSingleton<LiveSimulatorHost>();
        services.AddSingleton<ILiveControl>(sp => sp.GetRequiredService<LiveSimulatorHost>());
        services.AddHostedService(sp => sp.GetRequiredService<LiveSimulatorHost>());
        return services;
    }
}
