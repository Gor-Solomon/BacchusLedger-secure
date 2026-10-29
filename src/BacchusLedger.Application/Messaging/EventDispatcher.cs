using BacchusLedger.Application.Repositories;

namespace BacchusLedger.Application.Messaging;

public sealed class EventDispatcher(
    IServiceProvider services, OutboxEventTypes types, IOutboxRepository outbox, IUnitOfWork unitOfWork, IOutboxFaultInjector faults, TimeProvider clock)
    : IEventDispatcher
{
    public async Task DispatchAsync(OutboxMessage message, CancellationToken ct)
    {
        var e = types.Deserialize(message.Type, message.Payload);
        var errors = new List<string>();
        foreach (var handler in types.HandlersFor(e, services))
        {
            try
            {
                await unitOfWork.WriteAsync(async () =>
                {
                    if (await outbox.HasHandledAsync(message.Id, handler.Name)) return 0; // done in an earlier attempt: never twice
                    faults.BeforeHandle(handler.Name);
                    await handler.RunAsync(ct);
                    await outbox.MarkHandledAsync(message.Id, handler.Name, clock.GetUtcNow().UtcDateTime);
                    return 0;
                });
            }
            catch (Exception x) { errors.Add($"{handler.Name}: {x.Message}"); }
        }
        if (errors.Count > 0) throw new EventHandlingException(string.Join("; ", errors));
    }
}
