using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace BacchusLedger.Infrastructure.Persistence;

/// The cellar database. Table and column names are the original snake_case ones (see Configurations/).
public sealed class BacchusLedgerDbContext(DbContextOptions<BacchusLedgerDbContext> options) : DbContext(options)
{
    public DbSet<WineryEntity> Wineries => Set<WineryEntity>();
    public DbSet<FarmerEntity> Farmers => Set<FarmerEntity>();
    public DbSet<PlotEntity> Plots => Set<PlotEntity>();
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<DeliveryEntity> Deliveries => Set<DeliveryEntity>();
    public DbSet<VesselEntity> Vessels => Set<VesselEntity>();
    public DbSet<BatchEntity> Batches => Set<BatchEntity>();
    public DbSet<BatchOriginEntity> BatchOrigins => Set<BatchOriginEntity>();
    public DbSet<OperationEntity> Operations => Set<OperationEntity>();
    public DbSet<LabEntity> Lab => Set<LabEntity>();
    public DbSet<SettingEntity> Settings => Set<SettingEntity>();
    public DbSet<SensorReadingEntity> SensorReadings => Set<SensorReadingEntity>();
    public DbSet<DemoProgressEntity> DemoProgress => Set<DemoProgressEntity>();
    public DbSet<DemoRowEntity> DemoRows => Set<DemoRowEntity>();
    public DbSet<ReadingEntity> Readings => Set<ReadingEntity>();
    public DbSet<LiveEventEntity> LiveEvents => Set<LiveEventEntity>();
    public DbSet<MaintenanceEntity> Maintenance => Set<MaintenanceEntity>();
    public DbSet<SealChainEntity> SealChain => Set<SealChainEntity>();
    public DbSet<SealMetaEntity> SealMeta => Set<SealMetaEntity>();
    public DbSet<SealTamperEntity> SealTamper => Set<SealTamperEntity>();
    public DbSet<SealBatchEntity> SealBatches => Set<SealBatchEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<ProcessedMessageEntity> ProcessedMessages => Set<ProcessedMessageEntity>();

    /// Set by the outbox when it adds a message in the running transaction; the commit then wakes up the outbox worker.
    internal bool HasNewOutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BacchusLedgerDbContext).Assembly);

    /// The original schema has no indexes on foreign key columns; keep it that way.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
}
