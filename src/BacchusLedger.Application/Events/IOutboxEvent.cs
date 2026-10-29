namespace BacchusLedger.Application.Events;

/// An event written to the outbox (table outbox_messages) in the same transaction as the data it is about,
/// and handled later by the outbox worker.
public interface IOutboxEvent
{
    Guid EventId { get; }

    DateTime OccurredAt { get; }

    /// The vessel code the event is about: events of one vessel are handled in order. Null: no order needed.
    string? Vessel { get; }
}
