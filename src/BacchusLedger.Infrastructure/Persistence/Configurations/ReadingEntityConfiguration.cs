using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class ReadingEntityConfiguration : IEntityTypeConfiguration<ReadingEntity>
{
    public void Configure(EntityTypeBuilder<ReadingEntity> b)
    {
        b.ToTable("readings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.BatchId).HasColumnName("batch_id").HasColumnType("INTEGER");
        b.Property(x => x.VesselId).HasColumnName("vessel_id").HasColumnType("INTEGER");
        b.Property(x => x.TakenAt).HasColumnName("taken_at").HasColumnType("TEXT");
        b.Property(x => x.Type).HasColumnName("type").HasColumnType("TEXT");
        b.Property(x => x.Value).HasColumnName("value").HasColumnType("REAL");
        b.Property(x => x.Unit).HasColumnName("unit").HasColumnType("TEXT");
        b.Property(x => x.Instrument).HasColumnName("instrument").HasColumnType("TEXT");
        b.Property(x => x.Person).HasColumnName("person").HasColumnType("TEXT");
        b.Property(x => x.Note).HasColumnName("note").HasColumnType("TEXT");
        b.Property(x => x.Source).HasColumnName("source").HasColumnType("TEXT");
        b.HasOne<BatchEntity>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<VesselEntity>().WithMany().HasForeignKey(x => x.VesselId).OnDelete(DeleteBehavior.NoAction);
        b.ToTable(t => t.HasCheckConstraint("CK_readings_type", "type IN ('initial_brix','ph','temperature','hydrometer_density','hydrometer_abv','ta')"));
    }
}
