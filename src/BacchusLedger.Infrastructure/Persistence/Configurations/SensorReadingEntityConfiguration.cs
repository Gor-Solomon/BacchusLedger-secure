using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class SensorReadingEntityConfiguration : IEntityTypeConfiguration<SensorReadingEntity>
{
    public void Configure(EntityTypeBuilder<SensorReadingEntity> b)
    {
        b.ToTable("sensor_readings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.VesselId).HasColumnName("vessel_id").HasColumnType("INTEGER");
        b.Property(x => x.DipCm).HasColumnName("dip_cm").HasColumnType("REAL");
        b.Property(x => x.Litres).HasColumnName("litres").HasColumnType("REAL");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("TEXT");
        b.HasOne<VesselEntity>().WithMany().HasForeignKey(x => x.VesselId).OnDelete(DeleteBehavior.NoAction);
    }
}
