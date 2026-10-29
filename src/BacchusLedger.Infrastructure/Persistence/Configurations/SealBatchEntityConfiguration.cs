using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class SealBatchEntityConfiguration : IEntityTypeConfiguration<SealBatchEntity>
{
    public void Configure(EntityTypeBuilder<SealBatchEntity> b)
    {
        b.ToTable("seal_batch");
        b.HasKey(x => x.BatchId);
        b.Property(x => x.BatchId).ValueGeneratedNever();
        b.Property(x => x.BatchId).HasColumnName("batch_id").HasColumnType("INTEGER");
        b.Property(x => x.BatchCode).HasColumnName("batch_code").HasColumnType("TEXT");
        b.Property(x => x.FinalHash).HasColumnName("final_hash").HasColumnType("TEXT");
        b.Property(x => x.Signature).HasColumnName("signature").HasColumnType("TEXT");
        b.Property(x => x.SealedAt).HasColumnName("sealed_at").HasColumnType("TEXT");
        b.Property(x => x.Entries).HasColumnName("entries").HasColumnType("INTEGER");
        b.Property(x => x.KeyId).HasColumnName("key_id").HasColumnType("TEXT");
    }
}
