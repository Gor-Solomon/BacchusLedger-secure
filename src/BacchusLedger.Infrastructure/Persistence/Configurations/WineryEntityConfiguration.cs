using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class WineryEntityConfiguration : IEntityTypeConfiguration<WineryEntity>
{
    public void Configure(EntityTypeBuilder<WineryEntity> b)
    {
        b.ToTable("wineries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Code).HasColumnName("code").HasColumnType("TEXT");
        b.Property(x => x.Name).HasColumnName("name").HasColumnType("TEXT");
        b.Property(x => x.Size).HasColumnName("size").HasColumnType("TEXT");
        b.Property(x => x.YearlyT).HasColumnName("yearly_t").HasColumnType("REAL");
        b.Property(x => x.UsesQvevri).HasColumnName("uses_qvevri").HasColumnType("INTEGER");
        b.Property(x => x.ReadinessLevel).HasColumnName("readiness_level").HasColumnType("TEXT");
        b.HasIndex(x => x.Code).IsUnique();
    }
}
