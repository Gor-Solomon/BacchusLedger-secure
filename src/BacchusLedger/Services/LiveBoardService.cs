using System.Text;
using System.Text.Json;
using BacchusLedger.Contracts.Common;
using BacchusLedger.Contracts.Live;
using BacchusLedger.Extensions;
using BacchusLedger.Models;
using BacchusLedger.Options;
using BacchusLedger.Repositories;
using Microsoft.Extensions.Options;
using BacchusLedger.Domain.Vessels;

namespace BacchusLedger.Services;

public sealed class LiveBoardService(
    ILiveControl live, ILiveHub hub, ILiveEventRepository events, IVesselRepository vessels, IUnitOfWork unitOfWork, IOptions<LiveOptions> options) : ILiveBoardService
{
    public LiveStateDto State() => live.State();

    public async Task<LiveStateDto> TurnOnAsync()
    {
        await live.TurnOnAsync();
        await live.TickAsync();
        return live.State();
    }

    public async Task<LiveStateDto> TurnOffAsync()
    {
        await live.TurnOffAsync();
        return live.State();
    }

    public async Task<ServiceResult<LiveStateDto>> SetScenarioAsync(string name)
    {
        if (!LiveScenarios.All.Contains(name)) return ServiceResult<LiveStateDto>.NotFound("unknown scenario");
        await live.SetScenarioAsync(name);
        return live.State();
    }

    public async Task<IReadOnlyList<LiveEventRowDto>> EventsAsync(string? kind, string? vessel, int? limit) =>
        await events.ListAsync(kind, vessel, Math.Clamp(limit ?? 50, 1, 500));

    public async Task<IdResponse> AcknowledgeAsync(long id)
    {
        await unitOfWork.WriteAsync(async () => { await events.AcknowledgeAsync(id); return 0; });
        hub.Publish("ack", new IdResponse(id));
        return new IdResponse(id);
    }

    public async Task<OkResponse> AcknowledgeAllAsync()
    {
        await unitOfWork.WriteAsync(async () => { await events.AcknowledgeAllNotificationsAsync(); return 0; });
        hub.Publish("ack", new AckAllMessage(true));
        return new OkResponse(true);
    }

    public async Task<OkResponse> ClearAsync()
    {
        await live.TurnOffAsync();
        await unitOfWork.WriteAsync(async () => { await events.DeleteAllAsync(); return 0; });
        hub.Publish("cleared", new OkResponse(true));
        return new OkResponse(true);
    }

    public async Task StreamAsync(Func<string, Task> write, bool once, CancellationToken ct)
    {
        var (reader, dispose) = hub.Subscribe();
        try
        {
            await write($"retry: 3000\nevent: state\ndata: {hub.ToJson(live.State())}\n\n");
            if (once) return;
            while (!ct.IsCancellationRequested)
            {
                var wait = reader.WaitToReadAsync(ct).AsTask();
                if (await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(options.Value.KeepAliveSeconds), ct)) != wait) { await write(": keep-alive\n\n"); continue; }
                if (!await wait) break;
                var batch = new StringBuilder();
                while (reader.TryRead(out var msg)) batch.Append(msg);
                await write(batch.ToString());
            }
        }
        catch (OperationCanceledException) { }
        finally { dispose(); }
    }

    /// A real sensor feeds the same stream as the simulator; its events are tagged SENSOR, not SIMULATED.
    public async Task<ServiceResult<SensorReadingResponse>> SensorReadingAsync(JsonElement body)
    {
        var code = body.RawStr("vessel");
        if (code == null || body.Num("dip_cm") is not { } dip) return ServiceResult<SensorReadingResponse>.BadRequest("vessel and dip_cm are required");
        LiveEvent? ev = null;
        var res = await unitOfWork.WriteAsync(async () =>
        {
            if (await vessels.FindAsync(code, null) is not { } v) return ServiceResult<SensorReadingResponse>.NotFound("vessel not found");
            var m = VesselCalibration.TryParse(v.CalibrationJson);
            if (m == null) return ServiceResult<SensorReadingResponse>.BadRequest("Volume unknown — enter litres manually");
            var l = Math.Round(m.LitresAtDip(dip), 1);
            await events.InsertSensorReadingAsync(v.Id, dip, l, DateTime.UtcNow.ToString("o"));
            ev = new LiveEvent
            {
                Kind = "reading", VesselId = v.Id, VesselCode = v.Code, VesselName = v.CellarLabel ?? v.Code, Metric = "level", Value = l, Unit = "L", Source = "SENSOR",
                CreatedAt = DateTime.UtcNow.ToString("o"), SimTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm"),
            };
            ev.Id = await events.InsertAsync(ev);
            return ServiceResult<SensorReadingResponse>.Ok(new SensorReadingResponse(code, dip, l, "Calculated"));
        });
        if (ev != null) hub.Publish("sensor", ev);
        return res;
    }
}
