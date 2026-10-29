namespace BacchusLedger.Services.Assistant;

/// A provider answered, but not usefully (HTTP error, empty answer, too many tool rounds). The message is safe to log.
public sealed class AssistantException(string message) : Exception(message);
