namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table seal_chain: One sealed entry of a batch's hash chain.
public sealed class SealChainEntity
{
    public long BatchId { get; set; }
    public long Seq { get; set; }
    public string? Tbl { get; set; }
    public long? RowId { get; set; }
    public string? PrevHash { get; set; }
    public string? Hash { get; set; }
}
