using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class SettingEntityConfiguration : IEntityTypeConfiguration<SettingEntity>
{
    public void Configure(EntityTypeBuilder<SettingEntity> b)
    {
        b.ToTable("settings");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasColumnName("key").HasColumnType("TEXT");
        b.Property(x => x.Value).HasColumnName("value").HasColumnType("TEXT");
    }
}
