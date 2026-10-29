namespace BacchusLedger.Application.Events;

/// One simulator step for one vessel (SIMULATED). The range rules run on it later, in the outbox worker.
public sealed record LiveReadingSimulated(Guid EventId, DateTime OccurredAt, string? Vessel, SimulatedReadings Payload) : IOutboxEvent;
