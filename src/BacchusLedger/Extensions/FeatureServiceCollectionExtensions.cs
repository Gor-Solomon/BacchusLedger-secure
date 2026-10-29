using BacchusLedger.Infrastructure.Ai;
using BacchusLedger.Infrastructure.Live;
using BacchusLedger.Infrastructure.Persistence;
using BacchusLedger.Infrastructure.Providers;
using BacchusLedger.Providers;
using BacchusLedger.Repositories;
using BacchusLedger.Services;
using BacchusLedger.Services.Assistant;
using BacchusLedger.Services.SelfTest;

namespace BacchusLedger.Extensions;

/// DI registrations, one method per feature (called from Startup.ConfigureServices).
public static class FeatureServiceCollectionExtensions
{
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
        .AddSingleton<IDatasetWriter, DatasetWriter>()
        .AddSingleton<DatabaseSeeder>()
        .AddSingleton<IDatabaseInitializer, DatabaseInitializer>();

    public static IServiceCollection AddProviders(this IServiceCollection services)
    {
        services.AddSingleton<ISigningKeyProvider, PemSigningKeyProvider>();
        services.AddSingleton<IRangeConfigSource, FileRangeConfigSource>();
        services.AddSingleton<IFactsSource, FileFactsSource>();
        services.AddSingleton<IQrCodeRenderer, QrCoderRenderer>();
        services.AddSingleton<ILocalApiClient, LocalApiClient>();
        services.AddHttpClient("ai", c => c.Timeout = Timeout.InfiniteTimeSpan); // the per-question time limit is a CancellationToken
        services.AddHttpClient("local", c => c.Timeout = TimeSpan.FromSeconds(10));
        return services;
    }

    public static IServiceCollection AddHealthFeature(this IServiceCollection services) =>
        services.AddSingleton<IHealthService, HealthService>();

    public static IServiceCollection AddOverviewFeature(this IServiceCollection services) =>
        services.AddSingleton<IOverviewService, OverviewService>();

    public static IServiceCollection AddBatchesFeature(this IServiceCollection services) => services
        .AddSingleton<ILossAnalysisService, LossAnalysisService>()
        .AddSingleton<IBatchService, BatchService>()
        .AddSingleton<IOperationService, OperationService>()
        .AddSingleton<IOriginService, OriginService>();

    public static IServiceCollection AddVesselsFeature(this IServiceCollection services) => services
        .AddSingleton<IVesselService, VesselService>()
        .AddSingleton<ICellarService, CellarService>();

    public static IServiceCollection AddReadingsFeature(this IServiceCollection services) =>
        services.AddSingleton<IReadingService, ReadingService>();

    public static IServiceCollection AddJournalsFeature(this IServiceCollection services) =>
        services.AddSingleton<IJournalService, JournalService>();

    public static IServiceCollection AddReportFeature(this IServiceCollection services) =>
        services.AddSingleton<IReportService, ReportService>();

    public static IServiceCollection AddSealFeature(this IServiceCollection services) =>
        services.AddSingleton<ISealService, SealService>();

    public static IServiceCollection AddLiveFeature(this IServiceCollection services)
    {
        services.AddSingleton<ILiveHub, LiveHub>();
        services.AddSingleton<ILiveSimulatorFactory, LiveSimulatorFactory>();
        services.AddSingleton<LiveSimulatorHost>();
        services.AddSingleton<ILiveControl>(sp => sp.GetRequiredService<LiveSimulatorHost>());
        services.AddHostedService(sp => sp.GetRequiredService<LiveSimulatorHost>());
        services.AddSingleton<ILiveBoardService, LiveBoardService>();
        return services;
    }

    public static IServiceCollection AddAssistantFeature(this IServiceCollection services) => services
        .AddSingleton<IAiProvider, OpenAiProvider>()
        .AddSingleton<IAiProvider, GeminiProvider>()
        .AddSingleton<IAssistantTools, AssistantTools>()
        .AddSingleton<IQuickAnswerService, QuickAnswerService>()
        .AddSingleton<IAssistantService, AssistantService>();

    public static IServiceCollection AddDemoFeature(this IServiceCollection services) =>
        services.AddSingleton<IDemoService, DemoService>();

    public static IServiceCollection AddSettingsFeature(this IServiceCollection services) =>
        services.AddSingleton<ISettingsService, SettingsService>();

    /// Sections run in this order.
    public static IServiceCollection AddSelfTestFeature(this IServiceCollection services) => services
        .AddSingleton<ISelfTestSection, CoreSelfTest>()
        .AddSingleton<ISelfTestSection, LiveSelfTest>()
        .AddSingleton<ISelfTestSection, AssistantSelfTest>()
        .AddSingleton<ISelfTestSection, RangeSelfTest>()
        .AddSingleton<ISelfTestSection, LedgerSelfTest>()
        .AddSingleton<ISelfTestSection, SealSelfTest>()
        .AddSingleton<ISelfTestService, SelfTestService>();
}
