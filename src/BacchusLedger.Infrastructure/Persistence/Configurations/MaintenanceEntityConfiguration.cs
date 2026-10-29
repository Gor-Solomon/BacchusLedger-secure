using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class MaintenanceEntityConfiguration : IEntityTypeConfiguration<MaintenanceEntity>
{
    public void Configure(EntityTypeBuilder<MaintenanceEntity> b)
    {
        b.ToTable("maintenance");
        b.HasKey(x => x.Rowid);
        b.Property(x => x.Rowid).HasColumnName("rowid"); // implicit rowid: the migration does not declare this column
        b.Property(x => x.Reason).HasColumnName("reason").HasColumnType("TEXT");
    }
}
