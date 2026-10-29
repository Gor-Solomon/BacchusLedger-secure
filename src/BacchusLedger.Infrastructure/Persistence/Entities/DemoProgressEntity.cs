namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table demo_progress: A demo player step that has been written.
public sealed class DemoProgressEntity
{
    public string Script { get; set; } = "";
    public long Step { get; set; }
}
