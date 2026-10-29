using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class ProcessedMessageEntityConfiguration : IEntityTypeConfiguration<ProcessedMessageEntity>
{
    public void Configure(EntityTypeBuilder<ProcessedMessageEntity> b)
    {
        b.ToTable("processed_messages");
        b.HasKey(x => new { x.MessageId, x.Handler });
        b.Property(x => x.MessageId).HasColumnName("message_id");
        b.Property(x => x.Handler).HasColumnName("handler");
        b.Property(x => x.ProcessedAt).HasColumnName("processed_at").AsUtc();
    }
}
