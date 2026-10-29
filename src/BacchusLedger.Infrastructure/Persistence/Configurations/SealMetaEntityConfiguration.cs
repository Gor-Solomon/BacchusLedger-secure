using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class SealMetaEntityConfiguration : IEntityTypeConfiguration<SealMetaEntity>
{
    public void Configure(EntityTypeBuilder<SealMetaEntity> b)
    {
        b.ToTable("seal_meta");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasColumnName("key").HasColumnType("TEXT");
        b.Property(x => x.Value).HasColumnName("value").HasColumnType("TEXT");
    }
}
