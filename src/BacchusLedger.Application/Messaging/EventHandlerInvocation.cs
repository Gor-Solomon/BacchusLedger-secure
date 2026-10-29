namespace BacchusLedger.Application.Messaging;

/// One handler bound to one event: Name is the handler class (stored in processed_messages.handler).
public sealed record EventHandlerInvocation(string Name, Func<CancellationToken, Task> RunAsync);
