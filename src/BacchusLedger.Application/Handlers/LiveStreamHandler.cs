using BacchusLedger.Application.Contracts.Live;
using BacchusLedger.Application.Events;
using BacchusLedger.Application.Messaging;
using BacchusLedger.Application.Services;

namespace BacchusLedger.Application.Handlers;

/// Pushes live events to every open SSE stream (GET /api/live/stream) as the messages the UI already knows: "sensor" and "tick".
public sealed class LiveStreamHandler(ILiveHub hub, ILiveControl live)
    : IEventHandler<SensorReadingReceived>, IEventHandler<LiveReadingSimulated>, IEventHandler<RangeAlertRaised>
{
    public Task HandleAsync(SensorReadingReceived e, CancellationToken ct)
    {
        hub.Publish("sensor", e.Payload);
        return Task.CompletedTask;
    }

    public Task HandleAsync(LiveReadingSimulated e, CancellationToken ct)
    {
        hub.Publish("tick", new LiveTickMessage(live.State(), e.Payload.Readings, []));
        return Task.CompletedTask;
    }

    public Task HandleAsync(RangeAlertRaised e, CancellationToken ct)
    {
        hub.Publish("tick", new LiveTickMessage(live.State(), [], [e.Payload]));
        return Task.CompletedTask;
    }
}
