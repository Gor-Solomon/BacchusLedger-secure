namespace BacchusLedger.Application.Contracts.Assistant;

public sealed record AssistantStatusDto(bool AiConfigured, string StatusText, string Greeting, int MaxChars, IReadOnlyList<QuickQuestionDto> Quick);
