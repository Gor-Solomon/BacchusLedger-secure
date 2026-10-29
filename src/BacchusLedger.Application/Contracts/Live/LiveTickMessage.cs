using BacchusLedger.Application.Models;

namespace BacchusLedger.Application.Contracts.Live;

/// SSE "tick" message: the new state and the events of this simulator step.
public sealed record LiveTickMessage(LiveStateDto State, IReadOnlyList<LiveEvent> Readings, IReadOnlyList<LiveEvent> Notifications);
