namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table farmers: A grape farmer (DEMO ASSUMPTION in the seed data).
public sealed class FarmerEntity
{
    public long Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public long? IsDemo { get; set; }
}
