using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class DemoProgressEntityConfiguration : IEntityTypeConfiguration<DemoProgressEntity>
{
    public void Configure(EntityTypeBuilder<DemoProgressEntity> b)
    {
        b.ToTable("demo_progress");
        b.HasKey(x => new { x.Script, x.Step });
        b.Property(x => x.Script).HasColumnName("script").HasColumnType("TEXT");
        b.Property(x => x.Step).HasColumnName("step").HasColumnType("INTEGER");
    }
}
