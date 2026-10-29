using BacchusLedger.Application.Models;

namespace BacchusLedger.Application.Events;

/// A live notification (range alert, level drop, stuck, missing Brix, back to normal). Payload: the stored live event.
public sealed record RangeAlertRaised(Guid EventId, DateTime OccurredAt, string? Vessel, LiveEvent Payload) : IOutboxEvent
{
    public static RangeAlertRaised For(LiveEvent notification, DateTime occurredAt) => new(Guid.NewGuid(), occurredAt, notification.VesselCode, notification);
}
