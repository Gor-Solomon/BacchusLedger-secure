using BacchusLedger.Application.Events;

namespace BacchusLedger.Application.Models;

/// One simulator step before the rules: notifications waiting from a scenario change, and the readings of each streaming vessel.
public sealed record SimulatedTick(IReadOnlyList<LiveEvent> Notes, IReadOnlyList<LiveReadingSimulated> Readings);
