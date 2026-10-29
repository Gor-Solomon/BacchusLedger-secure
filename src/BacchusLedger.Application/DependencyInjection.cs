using BacchusLedger.Application.Events;
using BacchusLedger.Application.Handlers;
using BacchusLedger.Application.Messaging;
using BacchusLedger.Application.Services;
using BacchusLedger.Application.Services.Assistant;
using BacchusLedger.Application.Services.SelfTest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        .AddOutboxFeature()
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

    /// The event types stored in the outbox. The outbox itself (IOutbox) and its worker come from the infrastructure.
    /// Handlers of one event run in the order they are registered here: the rules before the live stream.
    public static IServiceCollection AddOutboxFeature(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        return services
            .AddSingleton(new OutboxEventTypes().Add<SensorReadingReceived>().Add<LiveReadingSimulated>().Add<RangeAlertRaised>())
            .AddSingleton<RetryPolicy>()
            .AddSingleton<IOutboxFaultInjector, OutboxFaultInjector>()
            .AddScoped<IEventDispatcher, EventDispatcher>()
            .AddScoped<IMessagePublisher, InProcessMessagePublisher>()
            .AddScoped<IOutboxBatchProcessor, OutboxBatchProcessor>()
            .AddScoped<RangeRulesHandler>()
            .AddEventHandler<LiveReadingSimulated, RangeRulesHandler>()
            .AddScoped<LiveStreamHandler>()
            .AddEventHandler<SensorReadingReceived, LiveStreamHandler>()
            .AddEventHandler<LiveReadingSimulated, LiveStreamHandler>()
            .AddEventHandler<RangeAlertRaised, LiveStreamHandler>();
    }

    /// Registers THandler (already registered itself) as a handler of TEvent.
    public static IServiceCollection AddEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IOutboxEvent where THandler : class, IEventHandler<TEvent> =>
        services.AddScoped<IEventHandler<TEvent>>(sp => sp.GetRequiredService<THandler>());

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
