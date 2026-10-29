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
        services.AddScoped<IHealthService, HealthService>();

    public static IServiceCollection AddOverviewFeature(this IServiceCollection services) =>
        services.AddScoped<IOverviewService, OverviewService>();

    public static IServiceCollection AddBatchesFeature(this IServiceCollection services) => services
        .AddScoped<ILossAnalysisService, LossAnalysisService>()
        .AddScoped<IDatasetWriter, DatasetWriter>()
        .AddScoped<IBatchService, BatchService>()
        .AddScoped<IOperationService, OperationService>()
        .AddScoped<IOriginService, OriginService>();

    public static IServiceCollection AddVesselsFeature(this IServiceCollection services) => services
        .AddScoped<IVesselService, VesselService>()
        .AddScoped<ICellarService, CellarService>();

    public static IServiceCollection AddReadingsFeature(this IServiceCollection services) =>
        services.AddScoped<IReadingService, ReadingService>();

    public static IServiceCollection AddJournalsFeature(this IServiceCollection services) =>
        services.AddScoped<IJournalService, JournalService>();

    public static IServiceCollection AddReportFeature(this IServiceCollection services) =>
        services.AddScoped<IReportService, ReportService>();

    public static IServiceCollection AddSealFeature(this IServiceCollection services) =>
        services.AddScoped<ISealService, SealService>();

    /// The simulator itself (ILiveControl, ILiveSimulatorFactory, ILiveHub) comes from the infrastructure.
    public static IServiceCollection AddLiveFeature(this IServiceCollection services) =>
        services.AddScoped<ILiveBoardService, LiveBoardService>();

    /// The AI providers (IAiProvider) come from the infrastructure.
    public static IServiceCollection AddAssistantFeature(this IServiceCollection services) => services
        .AddScoped<IAssistantTools, AssistantTools>()
        .AddScoped<IQuickAnswerService, QuickAnswerService>()
        .AddScoped<IAssistantService, AssistantService>();

    public static IServiceCollection AddDemoFeature(this IServiceCollection services) =>
        services.AddScoped<IDemoService, DemoService>();

    public static IServiceCollection AddSettingsFeature(this IServiceCollection services) =>
        services.AddScoped<ISettingsService, SettingsService>();

    /// Sections run in this order.
    public static IServiceCollection AddSelfTestFeature(this IServiceCollection services) => services
        .AddScoped<ISelfTestSection, CoreSelfTest>()
        .AddScoped<ISelfTestSection, LiveSelfTest>()
        .AddScoped<ISelfTestSection, AssistantSelfTest>()
        .AddScoped<ISelfTestSection, RangeSelfTest>()
        .AddScoped<ISelfTestSection, LedgerSelfTest>()
        .AddScoped<ISelfTestSection, SealSelfTest>()
        .AddScoped<ISelfTestService, SelfTestService>();
}
