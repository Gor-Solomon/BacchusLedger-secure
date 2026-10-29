using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class LiveEventEntityConfiguration : IEntityTypeConfiguration<LiveEventEntity>
{
    public void Configure(EntityTypeBuilder<LiveEventEntity> b)
    {
        b.ToTable("live_events");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("TEXT");
        b.Property(x => x.SimTime).HasColumnName("sim_time").HasColumnType("TEXT");
        b.Property(x => x.VesselId).HasColumnName("vessel_id").HasColumnType("INTEGER");
        b.Property(x => x.VesselCode).HasColumnName("vessel_code").HasColumnType("TEXT");
        b.Property(x => x.VesselName).HasColumnName("vessel_name").HasColumnType("TEXT");
        b.Property(x => x.Kind).HasColumnName("kind").HasColumnType("TEXT");
        b.Property(x => x.Metric).HasColumnName("metric").HasColumnType("TEXT");
        b.Property(x => x.Value).HasColumnName("value").HasColumnType("REAL");
        b.Property(x => x.Unit).HasColumnName("unit").HasColumnType("TEXT");
        b.Property(x => x.Level).HasColumnName("level").HasColumnType("TEXT");
        b.Property(x => x.Rule).HasColumnName("rule").HasColumnType("TEXT");
        b.Property(x => x.Message).HasColumnName("message").HasColumnType("TEXT");
        b.Property(x => x.ValueUsd).HasColumnName("value_usd").HasColumnType("REAL");
        b.Property(x => x.Scenario).HasColumnName("scenario").HasColumnType("TEXT");
        b.Property(x => x.Source).HasColumnName("source").HasColumnType("TEXT");
        b.Property(x => x.Acknowledged).HasColumnName("acknowledged").HasColumnType("INTEGER");
        b.Property(x => x.Acknowledged).HasDefaultValue(0L);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_live_events_kind", "kind IN ('reading','notification')");
            t.HasCheckConstraint("CK_live_events_source", "source IN ('SIMULATED','SENSOR')");
        });
    }
}
