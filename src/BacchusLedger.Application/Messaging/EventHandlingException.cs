namespace BacchusLedger.Application.Messaging;

/// One or more handlers of a message failed (the message is retried; handlers that succeeded are not run again).
public sealed class EventHandlingException(string message) : Exception(message);
