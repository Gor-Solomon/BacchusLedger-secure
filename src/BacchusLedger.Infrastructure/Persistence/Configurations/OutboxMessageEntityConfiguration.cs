using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<OutboxMessageEntity> b)
    {
        b.ToTable("outbox_messages", t => t.HasCheckConstraint("CK_outbox_messages_status", "status IN ('pending','processed','failed')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Type).HasColumnName("type");
        b.Property(x => x.Payload).HasColumnName("payload");
        b.Property(x => x.OccurredAt).HasColumnName("occurred_at").AsUtc();
        b.Property(x => x.Vessel).HasColumnName("vessel");
        b.Property(x => x.Seq).HasColumnName("seq");
        b.Property(x => x.Status).HasColumnName("status").HasDefaultValue("pending");
        b.Property(x => x.Attempts).HasColumnName("attempts").HasDefaultValue(0);
        b.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at").AsUtc();
        b.Property(x => x.LastError).HasColumnName("last_error");
        b.Property(x => x.ProcessedAt).HasColumnName("processed_at").AsUtc();
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
        b.HasIndex(x => x.Seq).IsUnique();
    }
}
