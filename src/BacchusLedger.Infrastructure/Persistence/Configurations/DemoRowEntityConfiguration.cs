using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class DemoRowEntityConfiguration : IEntityTypeConfiguration<DemoRowEntity>
{
    public void Configure(EntityTypeBuilder<DemoRowEntity> b)
    {
        b.ToTable("demo_rows");
        b.HasKey(x => x.Rowid);
        b.Property(x => x.Rowid).HasColumnName("rowid"); // implicit rowid: the migration does not declare this column
        b.Property(x => x.Script).HasColumnName("script").HasColumnType("TEXT");
        b.Property(x => x.Tbl).HasColumnName("tbl").HasColumnType("TEXT");
        b.Property(x => x.RowId).HasColumnName("row_id").HasColumnType("INTEGER");
    }
}
