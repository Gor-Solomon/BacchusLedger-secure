namespace BacchusLedger.Contracts.Demo;

/// What a demo step received (with its source).
public sealed record DemoFieldDto(string Label, string Value, string Source);
