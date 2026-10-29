namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table lab: A laboratory analysis row (sealed in the hash chain).
public sealed class LabEntity
{
    public long Id { get; set; }
    public long? BatchId { get; set; }
    public string? Date { get; set; }
    public long? VesselId { get; set; }
    public string? Stage { get; set; }
    public double? SugarGl { get; set; }
    public double? TempC { get; set; }
    public double? Ph { get; set; }
    public double? TaGl { get; set; }
    public double? RsGl { get; set; }
    public string? Note { get; set; }
    public string? Source { get; set; }
}
