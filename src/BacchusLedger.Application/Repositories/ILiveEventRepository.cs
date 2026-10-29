using BacchusLedger.Application.Contracts.Live;
using BacchusLedger.Application.Contracts.Report;
using BacchusLedger.Application.Models;

namespace BacchusLedger.Application.Repositories;

/// Simulated (or sensor-stub) live events. Never read by journals, the loss engine or ROI numbers.
public interface ILiveEventRepository
{
    Task<List<LiveEventRowDto>> ListAsync(string? kind, string? vesselCode, int limit);

    /// Unacknowledged red and amber notifications, newest first.
    Task<List<LiveEventRowDto>> ListOpenAlertsAsync(int limit);

    Task<List<SimulatedAlertDto>> ListVesselNotificationsAsync(long vesselId);
    Task<long> CountVesselReadingsAsync(long vesselId);
    Task<long> InsertAsync(LiveEvent e);
    Task AcknowledgeAsync(long id);
    Task AcknowledgeAllNotificationsAsync();
    Task DeleteAllAsync();

    /// Keeps only the newest readings (notifications stay).
    Task TrimReadingsAsync(int keep);

    Task<long> CountSimulatedAsync();

    /// Events with a source other than SIMULATED or SENSOR.
    Task<long> CountUntaggedAsync();

    Task InsertSensorReadingAsync(long vesselId, double dipCm, double litres, string createdAt);
}
