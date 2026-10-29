using System.Text.Json;
using BacchusLedger.Contracts.Common;
using BacchusLedger.Contracts.Live;

namespace BacchusLedger.Services;

/// LIVE mode (SIMULATED): simulator control, events, the SSE stream and the sensor stub.
public interface ILiveBoardService
{
    LiveStateDto State();
    LiveStateDto TurnOn();
    LiveStateDto TurnOff();
    ServiceResult<LiveStateDto> SetScenario(string name);
    Task<IReadOnlyList<LiveEventRowDto>> EventsAsync(string? kind, string? vessel, int? limit);
    Task<IdResponse> AcknowledgeAsync(long id);
    Task<OkResponse> AcknowledgeAllAsync();

    /// Simulated data is not a record: clearing it is allowed (it never reaches journals or ROI).
    Task<OkResponse> ClearAsync();

    /// Writes the SSE stream (state first, then every published message) until the client goes away; once = state only.
    Task StreamAsync(Func<string, Task> write, bool once, CancellationToken ct);

    /// Sensor stub: dip → calibrated litres, fed into the live stream tagged SENSOR.
    Task<ServiceResult<SensorReadingResponse>> SensorReadingAsync(JsonElement body);
}
