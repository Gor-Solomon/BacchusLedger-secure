namespace BacchusLedger.Domain.Losses;

/// One cellar operation as replayed by the loss calculator (vessel codes and cellar labels included).
public sealed class CellarOperation
{
    public long Id { get; set; }
    public string Date { get; init; } = "";
    public string Type { get; init; } = "";
    public long? FromId { get; init; }
    public long? ToId { get; init; }
    public string? FromCode { get; init; }
    public string? ToCode { get; init; }
    public string? FromLabel { get; init; }
    public string? ToLabel { get; init; }
    public string? Method { get; init; }
    public string? Note { get; init; }
    public string? Source { get; init; }
    public string? Person { get; init; }
    public double? Litres { get; init; }
    public double? Ml { get; init; }
    public double? Remainder { get; init; }
    public double? Dip { get; init; }
    public int? Bottles { get; init; }
}
