using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class SealChainEntityConfiguration : IEntityTypeConfiguration<SealChainEntity>
{
    public void Configure(EntityTypeBuilder<SealChainEntity> b)
    {
        b.ToTable("seal_chain");
        b.HasKey(x => new { x.BatchId, x.Seq });
        b.Property(x => x.BatchId).HasColumnName("batch_id").HasColumnType("INTEGER");
        b.Property(x => x.Seq).HasColumnName("seq").HasColumnType("INTEGER");
        b.Property(x => x.Tbl).HasColumnName("tbl").HasColumnType("TEXT");
        b.Property(x => x.RowId).HasColumnName("row_id").HasColumnType("INTEGER");
        b.Property(x => x.PrevHash).HasColumnName("prev_hash").HasColumnType("TEXT");
        b.Property(x => x.Hash).HasColumnName("hash").HasColumnType("TEXT");
    }
}
