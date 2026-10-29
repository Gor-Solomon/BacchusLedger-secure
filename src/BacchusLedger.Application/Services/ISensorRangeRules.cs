using BacchusLedger.Application.Models;

namespace BacchusLedger.Application.Services;

/// Range alerts on real sensor readings (config/ranges.json, 3 out of range in a row, clear when back in range).
/// Kept apart from the simulator's alert state: SENSOR and SIMULATED readings never count towards each other's alerts.
public interface ISensorRangeRules
{
    /// The new notifications (tagged SENSOR, not stored yet) for one stored sensor reading of this vessel.
    IReadOnlyList<LiveEvent> Check(LiveEvent reading, VesselRecord vessel, string? batchCode);
}
