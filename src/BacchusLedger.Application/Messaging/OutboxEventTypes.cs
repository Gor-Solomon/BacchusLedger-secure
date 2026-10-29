using System.Text.Json;
using BacchusLedger.Application.Events;
using Microsoft.Extensions.DependencyInjection;

namespace BacchusLedger.Application.Messaging;

/// The event types the outbox knows: the name stored in outbox_messages.type, the JSON payload, and the handlers of each type.
public sealed class OutboxEventTypes
{
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    readonly Dictionary<string, Type> types = [];
    readonly Dictionary<Type, Func<IServiceProvider, IOutboxEvent, IEnumerable<EventHandlerInvocation>>> handlers = [];

    /// Registers TEvent; its handlers are every IEventHandler&lt;TEvent&gt; in DI, in registration order.
    public OutboxEventTypes Add<TEvent>() where TEvent : IOutboxEvent
    {
        types[typeof(TEvent).Name] = typeof(TEvent);
        handlers[typeof(TEvent)] = (services, e) => services.GetServices<IEventHandler<TEvent>>()
            .Select(h => new EventHandlerInvocation(h.GetType().Name, ct => h.HandleAsync((TEvent)e, ct)));
        return this;
    }

    public string NameOf(IOutboxEvent e) => types.ContainsValue(e.GetType()) ? e.GetType().Name : throw new ArgumentException($"unknown outbox event type {e.GetType().Name}");

    public string Serialize(IOutboxEvent e) => JsonSerializer.Serialize(e, e.GetType(), Json);

    public IOutboxEvent Deserialize(string type, string payload) =>
        types.TryGetValue(type, out var t) ? (IOutboxEvent)JsonSerializer.Deserialize(payload, t, Json)! : throw new ArgumentException($"unknown outbox event type {type}");

    public IEnumerable<EventHandlerInvocation> HandlersFor(IOutboxEvent e, IServiceProvider services) => handlers[e.GetType()](services, e);
}
