using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class VesselEntityConfiguration : IEntityTypeConfiguration<VesselEntity>
{
    public void Configure(EntityTypeBuilder<VesselEntity> b)
    {
        b.ToTable("vessels");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.WineryId).HasColumnName("winery_id").HasColumnType("INTEGER");
        b.Property(x => x.Code).HasColumnName("code").HasColumnType("TEXT");
        b.Property(x => x.Type).HasColumnName("type").HasColumnType("TEXT");
        b.Property(x => x.Name).HasColumnName("name").HasColumnType("TEXT");
        b.Property(x => x.CalibrationJson).HasColumnName("calibration_json").HasColumnType("TEXT");
        b.Property(x => x.CapacityL).HasColumnName("capacity_l").HasColumnType("REAL");
        b.Property(x => x.Source).HasColumnName("source").HasColumnType("TEXT");
        b.Property(x => x.CellarLabel).HasColumnName("cellar_label").HasColumnType("TEXT");
        b.Property(x => x.CellarLabelSource).HasColumnName("cellar_label_source").HasColumnType("TEXT");
        b.Property(x => x.DepthCm).HasColumnName("depth_cm").HasColumnType("REAL");
        b.Property(x => x.NeckDCm).HasColumnName("neck_d_cm").HasColumnType("REAL");
        b.Property(x => x.MaxDCm).HasColumnName("max_d_cm").HasColumnType("REAL");
        b.Property(x => x.KnownVolumeL).HasColumnName("known_volume_l").HasColumnType("REAL");
        b.HasIndex(x => new { x.WineryId, x.Code }).IsUnique();
        b.HasOne<WineryEntity>().WithMany().HasForeignKey(x => x.WineryId).OnDelete(DeleteBehavior.NoAction);
        b.ToTable(t => t.HasCheckConstraint("CK_vessels_type", "type IN ('tank','qvevri')"));
    }
}
