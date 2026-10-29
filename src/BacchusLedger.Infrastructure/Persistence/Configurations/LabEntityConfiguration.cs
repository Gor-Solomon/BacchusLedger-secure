using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class LabEntityConfiguration : IEntityTypeConfiguration<LabEntity>
{
    public void Configure(EntityTypeBuilder<LabEntity> b)
    {
        b.ToTable("lab");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.BatchId).HasColumnName("batch_id").HasColumnType("INTEGER");
        b.Property(x => x.Date).HasColumnName("date").HasColumnType("TEXT");
        b.Property(x => x.VesselId).HasColumnName("vessel_id").HasColumnType("INTEGER");
        b.Property(x => x.Stage).HasColumnName("stage").HasColumnType("TEXT");
        b.Property(x => x.SugarGl).HasColumnName("sugar_gl").HasColumnType("REAL");
        b.Property(x => x.TempC).HasColumnName("temp_c").HasColumnType("REAL");
        b.Property(x => x.Ph).HasColumnName("ph").HasColumnType("REAL");
        b.Property(x => x.TaGl).HasColumnName("ta_gl").HasColumnType("REAL");
        b.Property(x => x.RsGl).HasColumnName("rs_gl").HasColumnType("REAL");
        b.Property(x => x.Note).HasColumnName("note").HasColumnType("TEXT");
        b.Property(x => x.Source).HasColumnName("source").HasColumnType("TEXT");
        b.HasOne<BatchEntity>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<VesselEntity>().WithMany().HasForeignKey(x => x.VesselId).OnDelete(DeleteBehavior.NoAction);
    }
}
