using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class FarmerEntityConfiguration : IEntityTypeConfiguration<FarmerEntity>
{
    public void Configure(EntityTypeBuilder<FarmerEntity> b)
    {
        b.ToTable("farmers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Code).HasColumnName("code").HasColumnType("TEXT");
        b.Property(x => x.Name).HasColumnName("name").HasColumnType("TEXT");
        b.Property(x => x.Phone).HasColumnName("phone").HasColumnType("TEXT");
        b.Property(x => x.IsDemo).HasColumnName("is_demo").HasColumnType("INTEGER");
        b.HasIndex(x => x.Code).IsUnique();
    }
}
