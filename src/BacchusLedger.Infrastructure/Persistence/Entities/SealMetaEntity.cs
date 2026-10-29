namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table seal_meta: Seal settings (the chain format version).
public sealed class SealMetaEntity
{
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}
