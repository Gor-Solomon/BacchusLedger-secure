using BacchusLedger.Application.Models;

namespace BacchusLedger.Application.Events;

/// The readings of one vessel in one simulator step, with the facts the rules need, taken when the readings were made.
/// OperationRecorded: an operation was recorded on the vessel since Live started (at most 1 h ago).
/// MissingBrixTarget: in the "Missing data" scenario, this vessel is the one without Initial Brix.
public sealed record SimulatedReadings(
    DateTime SimTime, string Scenario, IReadOnlyList<LiveEvent> Readings, bool OperationRecorded, bool MissingBrixTarget);
