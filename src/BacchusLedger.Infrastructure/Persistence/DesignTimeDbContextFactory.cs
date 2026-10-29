using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BacchusLedger.Infrastructure.Persistence;

/// Used only by the dotnet-ef tool (dotnet ef migrations add …), never at runtime.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BacchusLedgerDbContext>
{
    public BacchusLedgerDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<BacchusLedgerDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
