namespace BacchusLedger.Contracts.Assistant;

/// Mode: quick | ai | offline | not_configured | invalid.
public sealed record AssistantAnswer
{
    public string Text { get; init; } = "";
    public List<string> Sources { get; init; } = [];
    public string? Link { get; init; }
    public string? LinkLabel { get; init; }
    public List<string> Used { get; init; } = [];
    public string Mode { get; init; } = "quick";
    public string? Provider { get; init; }
}
