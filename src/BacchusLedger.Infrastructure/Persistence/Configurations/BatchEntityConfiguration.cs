using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class BatchEntityConfiguration : IEntityTypeConfiguration<BatchEntity>
{
    public void Configure(EntityTypeBuilder<BatchEntity> b)
    {
        b.ToTable("batches");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.WineryId).HasColumnName("winery_id").HasColumnType("INTEGER");
        b.Property(x => x.Code).HasColumnName("code").HasColumnType("TEXT");
        b.Property(x => x.Name).HasColumnName("name").HasColumnType("TEXT");
        b.Property(x => x.WineType).HasColumnName("wine_type").HasColumnType("TEXT");
        b.Property(x => x.Technology).HasColumnName("technology").HasColumnType("TEXT");
        b.Property(x => x.Variety).HasColumnName("variety").HasColumnType("TEXT");
        b.Property(x => x.Vintage).HasColumnName("vintage").HasColumnType("INTEGER");
        b.Property(x => x.Abv).HasColumnName("abv").HasColumnType("REAL");
        b.Property(x => x.Status).HasColumnName("status").HasColumnType("TEXT");
        b.Property(x => x.SourceNote).HasColumnName("source_note").HasColumnType("TEXT");
        b.Property(x => x.GrapesKg).HasColumnName("grapes_kg").HasColumnType("REAL");
        b.HasIndex(x => x.Code).IsUnique();
        b.HasOne<WineryEntity>().WithMany().HasForeignKey(x => x.WineryId).OnDelete(DeleteBehavior.NoAction);
        b.ToTable(t => t.HasCheckConstraint("CK_batches_technology", "technology IN ('classic','qvevri')"));
    }
}
