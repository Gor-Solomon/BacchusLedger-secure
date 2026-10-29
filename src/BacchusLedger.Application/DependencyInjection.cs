using BacchusLedger.Application.Services;
using BacchusLedger.Application.Services.Assistant;
using BacchusLedger.Application.Services.SelfTest;
using Microsoft.Extensions.DependencyInjection;

namespace BacchusLedger.Application;

/// Application services, one method per feature.
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) => services
        .AddHealthFeature()
        .AddOverviewFeature()
        .AddBatchesFeature()
        .AddVesselsFeature()
        .AddReadingsFeature()
        .AddJournalsFeature()
        .AddReportFeature()
        .AddSealFeature()
        .AddLiveFeature()
        .AddAssistantFeature()
        .AddDemoFeature()
        .AddSettingsFeature()
        .AddSelfTestFeature();

    public static IServiceCollection AddHealthFeature(this IServiceCollection services) =>
        services.AddSingleton<IHealthService, HealthService>();

    public static IServiceCollection AddOverviewFeature(this IServiceCollection services) =>
        services.AddSingleton<IOverviewService, OverviewService>();

    public static IServiceCollection AddBatchesFeature(this IServiceCollection services) => services
        .AddSingleton<ILossAnalysisService, LossAnalysisService>()
        .AddSingleton<IDatasetWriter, DatasetWriter>()
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

    /// The simulator itself (ILiveControl, ILiveSimulatorFactory, ILiveHub) comes from the infrastructure.
    public static IServiceCollection AddLiveFeature(this IServiceCollection services) =>
        services.AddSingleton<ILiveBoardService, LiveBoardService>();

    /// The AI providers (IAiProvider) come from the infrastructure.
    public static IServiceCollection AddAssistantFeature(this IServiceCollection services) => services
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
