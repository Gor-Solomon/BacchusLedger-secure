using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BacchusLedger.Infrastructure.Persistence.Configurations;

public static class UtcDateTimeExtensions
{
    /// SQLite keeps a DateTime as text without its kind: the outbox stores UTC only, so it is read back as UTC.
    public static PropertyBuilder<DateTime> AsUtc(this PropertyBuilder<DateTime> p) =>
        p.HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    public static PropertyBuilder<DateTime?> AsUtc(this PropertyBuilder<DateTime?> p) =>
        p.HasConversion(v => v, v => v == null ? null : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));
}
