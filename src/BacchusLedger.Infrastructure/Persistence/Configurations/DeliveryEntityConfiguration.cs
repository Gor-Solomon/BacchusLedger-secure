using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class DeliveryEntityConfiguration : IEntityTypeConfiguration<DeliveryEntity>
{
    public void Configure(EntityTypeBuilder<DeliveryEntity> b)
    {
        b.ToTable("deliveries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.OrderId).HasColumnName("order_id").HasColumnType("INTEGER");
        b.Property(x => x.PlotId).HasColumnName("plot_id").HasColumnType("INTEGER");
        b.Property(x => x.FarmerId).HasColumnName("farmer_id").HasColumnType("INTEGER");
        b.Property(x => x.Date).HasColumnName("date").HasColumnType("TEXT");
        b.Property(x => x.Kg).HasColumnName("kg").HasColumnType("REAL");
        b.Property(x => x.SugarGl).HasColumnName("sugar_gl").HasColumnType("REAL");
        b.Property(x => x.QualityNote).HasColumnName("quality_note").HasColumnType("TEXT");
        b.Property(x => x.Accepted).HasColumnName("accepted").HasColumnType("INTEGER");
        b.Property(x => x.Source).HasColumnName("source").HasColumnType("TEXT");
        b.HasOne<OrderEntity>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<PlotEntity>().WithMany().HasForeignKey(x => x.PlotId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<FarmerEntity>().WithMany().HasForeignKey(x => x.FarmerId).OnDelete(DeleteBehavior.NoAction);
    }
}
