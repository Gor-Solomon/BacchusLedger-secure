namespace BacchusLedger.Services.Assistant;

public sealed record AssistantToolParam(string Name, string Type, string Description, bool Required = false);
