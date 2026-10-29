using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class BatchOriginEntityConfiguration : IEntityTypeConfiguration<BatchOriginEntity>
{
    public void Configure(EntityTypeBuilder<BatchOriginEntity> b)
    {
        b.ToTable("batch_origins");
        b.HasKey(x => x.Rowid);
        b.Property(x => x.Rowid).HasColumnName("rowid"); // implicit rowid: the migration does not declare this column
        b.Property(x => x.BatchId).HasColumnName("batch_id").HasColumnType("INTEGER");
        b.Property(x => x.DeliveryId).HasColumnName("delivery_id").HasColumnType("INTEGER");
        b.Property(x => x.Kg).HasColumnName("kg").HasColumnType("REAL");
        b.HasOne<BatchEntity>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<DeliveryEntity>().WithMany().HasForeignKey(x => x.DeliveryId).OnDelete(DeleteBehavior.NoAction);
    }
}
