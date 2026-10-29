namespace BacchusLedger.Contracts.Demo;

/// What a demo step did. Status: pass | fail | info | above | null.
public sealed record DemoDidDto(string Text, string Source, string? Status = null);
