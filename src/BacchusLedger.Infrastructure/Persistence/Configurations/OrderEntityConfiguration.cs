using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class OrderEntityConfiguration : IEntityTypeConfiguration<OrderEntity>
{
    public void Configure(EntityTypeBuilder<OrderEntity> b)
    {
        b.ToTable("orders");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.WineryId).HasColumnName("winery_id").HasColumnType("INTEGER");
        b.Property(x => x.FarmerId).HasColumnName("farmer_id").HasColumnType("INTEGER");
        b.Property(x => x.PlotId).HasColumnName("plot_id").HasColumnType("INTEGER");
        b.Property(x => x.Variety).HasColumnName("variety").HasColumnType("TEXT");
        b.Property(x => x.KgPlanned).HasColumnName("kg_planned").HasColumnType("REAL");
        b.Property(x => x.PriceGelPerKg).HasColumnName("price_gel_per_kg").HasColumnType("REAL");
        b.Property(x => x.Date).HasColumnName("date").HasColumnType("TEXT");
        b.Property(x => x.IsDemo).HasColumnName("is_demo").HasColumnType("INTEGER");
        b.Property(x => x.Source).HasColumnName("source").HasColumnType("TEXT");
        b.HasOne<WineryEntity>().WithMany().HasForeignKey(x => x.WineryId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<FarmerEntity>().WithMany().HasForeignKey(x => x.FarmerId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<PlotEntity>().WithMany().HasForeignKey(x => x.PlotId).OnDelete(DeleteBehavior.NoAction);
    }
}
