using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class OperationEntityConfiguration : IEntityTypeConfiguration<OperationEntity>
{
    public void Configure(EntityTypeBuilder<OperationEntity> b)
    {
        b.ToTable("operations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.BatchId).HasColumnName("batch_id").HasColumnType("INTEGER");
        b.Property(x => x.Date).HasColumnName("date").HasColumnType("TEXT");
        b.Property(x => x.Type).HasColumnName("type").HasColumnType("TEXT");
        b.Property(x => x.FromVesselId).HasColumnName("from_vessel_id").HasColumnType("INTEGER");
        b.Property(x => x.ToVesselId).HasColumnName("to_vessel_id").HasColumnType("INTEGER");
        b.Property(x => x.Litres).HasColumnName("litres").HasColumnType("REAL");
        b.Property(x => x.Bottles).HasColumnName("bottles").HasColumnType("INTEGER");
        b.Property(x => x.BottleMl).HasColumnName("bottle_ml").HasColumnType("REAL");
        b.Property(x => x.RemainderL).HasColumnName("remainder_l").HasColumnType("REAL");
        b.Property(x => x.Method).HasColumnName("method").HasColumnType("TEXT");
        b.Property(x => x.DipCm).HasColumnName("dip_cm").HasColumnType("REAL");
        b.Property(x => x.Person).HasColumnName("person").HasColumnType("TEXT");
        b.Property(x => x.Note).HasColumnName("note").HasColumnType("TEXT");
        b.Property(x => x.Source).HasColumnName("source").HasColumnType("TEXT");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("TEXT");
        b.HasOne<BatchEntity>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<VesselEntity>().WithMany().HasForeignKey(x => x.FromVesselId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<VesselEntity>().WithMany().HasForeignKey(x => x.ToVesselId).OnDelete(DeleteBehavior.NoAction);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_operations_type", "type IN ('intake','separation','racking','stock_check','filtration','transfer','bottling','warehouse')");
            t.HasCheckConstraint("CK_operations_method", "method IS NULL OR method IN ('paper log','dip','bottle count','scale')");
        });
    }
}
