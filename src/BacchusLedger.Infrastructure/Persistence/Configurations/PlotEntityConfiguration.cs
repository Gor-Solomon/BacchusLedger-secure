using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class PlotEntityConfiguration : IEntityTypeConfiguration<PlotEntity>
{
    public void Configure(EntityTypeBuilder<PlotEntity> b)
    {
        b.ToTable("plots");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.FarmerId).HasColumnName("farmer_id").HasColumnType("INTEGER");
        b.Property(x => x.WineryId).HasColumnName("winery_id").HasColumnType("INTEGER");
        b.Property(x => x.Code).HasColumnName("code").HasColumnType("TEXT");
        b.Property(x => x.CadastreNo).HasColumnName("cadastre_no").HasColumnType("TEXT");
        b.Property(x => x.Village).HasColumnName("village").HasColumnType("TEXT");
        b.Property(x => x.Municipality).HasColumnName("municipality").HasColumnType("TEXT");
        b.Property(x => x.Region).HasColumnName("region").HasColumnType("TEXT");
        b.Property(x => x.AreaHa).HasColumnName("area_ha").HasColumnType("REAL");
        b.Property(x => x.Variety).HasColumnName("variety").HasColumnType("TEXT");
        b.Property(x => x.Lat).HasColumnName("lat").HasColumnType("REAL");
        b.Property(x => x.Lng).HasColumnName("lng").HasColumnType("REAL");
        b.Property(x => x.LicenceNo).HasColumnName("licence_no").HasColumnType("TEXT");
        b.Property(x => x.SoilTexture).HasColumnName("soil_texture").HasColumnType("TEXT");
        b.Property(x => x.SoilPh).HasColumnName("soil_ph").HasColumnType("REAL");
        b.Property(x => x.RainMm).HasColumnName("rain_mm").HasColumnType("REAL");
        b.Property(x => x.SummerTempC).HasColumnName("summer_temp_c").HasColumnType("REAL");
        b.Property(x => x.IsDemo).HasColumnName("is_demo").HasColumnType("INTEGER");
        b.HasIndex(x => x.Code).IsUnique();
        b.HasOne<FarmerEntity>().WithMany().HasForeignKey(x => x.FarmerId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<WineryEntity>().WithMany().HasForeignKey(x => x.WineryId).OnDelete(DeleteBehavior.NoAction);
    }
}
