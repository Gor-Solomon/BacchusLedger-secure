using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class SealTamperEntityConfiguration : IEntityTypeConfiguration<SealTamperEntity>
{
    public void Configure(EntityTypeBuilder<SealTamperEntity> b)
    {
        b.ToTable("seal_tamper");
        b.HasKey(x => x.Rowid);
        b.Property(x => x.Rowid).HasColumnName("rowid"); // implicit rowid: the migration does not declare this column
        b.Property(x => x.Tbl).HasColumnName("tbl").HasColumnType("TEXT");
        b.Property(x => x.RowId).HasColumnName("row_id").HasColumnType("INTEGER");
        b.Property(x => x.Col).HasColumnName("col").HasColumnType("TEXT");
        b.Property(x => x.OldValue).HasColumnName("old_value").HasColumnType("REAL");
        b.Property(x => x.BatchId).HasColumnName("batch_id").HasColumnType("INTEGER");
    }
}
