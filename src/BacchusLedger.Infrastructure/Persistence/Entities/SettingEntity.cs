namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table settings: One editable setting (key → text value).
public sealed class SettingEntity
{
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}
