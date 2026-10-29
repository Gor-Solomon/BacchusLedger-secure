using BacchusLedger.Application.Models;

namespace BacchusLedger.Application.Events;

/// A real sensor sent a dip (POST /api/sensors/reading). Payload: the stored live event (tagged SENSOR).
public sealed record SensorReadingReceived(Guid EventId, DateTime OccurredAt, string? Vessel, LiveEvent Payload) : IOutboxEvent
{
    public static SensorReadingReceived For(LiveEvent reading, DateTime occurredAt) => new(Guid.NewGuid(), occurredAt, reading.VesselCode, reading);
}
