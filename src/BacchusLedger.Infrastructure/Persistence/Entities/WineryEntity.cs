namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table wineries: A winery (Dataset A2/A7).
public sealed class WineryEntity
{
    public long Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Size { get; set; }
    public double? YearlyT { get; set; }
    public long? UsesQvevri { get; set; }
    public string? ReadinessLevel { get; set; }
}
