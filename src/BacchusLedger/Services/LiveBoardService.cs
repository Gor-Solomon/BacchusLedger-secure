using System.Text.Json;
using BacchusLedger.Contracts.Common;
using BacchusLedger.Contracts.Live;
using BacchusLedger.Extensions;

namespace BacchusLedger.Services;

public sealed class LiveBoardService(LiveService live, LiveHub hub) : ILiveBoardService
{
    public LiveStateDto State() => live.Sim.State();

    public LiveStateDto TurnOn()
    {
        live.TurnOn();
        live.Tick();
        return live.Sim.State();
    }

    public LiveStateDto TurnOff()
    {
        live.TurnOff();
        return live.Sim.State();
    }

    public ServiceResult<LiveStateDto> SetScenario(string name)
    {
        if (!LiveSim.Scenarios.Contains(name)) return ServiceResult<LiveStateDto>.NotFound("unknown scenario");
        live.SetScenario(name);
        return live.Sim.State();
    }

    public Task<IReadOnlyList<LiveEventRowDto>> EventsAsync(string? kind, string? vessel, int? limit) => Task.FromResult<IReadOnlyList<LiveEventRowDto>>(Db.Query("""
        SELECT * FROM live_events WHERE ($1 IS NULL OR kind=$1) AND ($2 IS NULL OR vessel_code=$2) ORDER BY id DESC LIMIT $3
        """, kind, vessel, Math.Clamp(limit ?? 50, 1, 500)).Select(e => new LiveEventRowDto((long)e["id"]!, e["created_at"] as string, e["sim_time"] as string, Db.L(e["vessel_id"]),
            e["vessel_code"] as string, e["vessel_name"] as string, e["kind"] as string, e["metric"] as string, Db.D(e["value"]), e["unit"] as string, e["level"] as string,
            e["rule"] as string, e["message"] as string, Db.D(e["value_usd"]), e["scenario"] as string, e["source"] as string, Db.L(e["acknowledged"]))).ToList());

    public Task<IdResponse> AcknowledgeAsync(long id)
    {
        Db.Write((c, tx) => Db.Exec(c, tx, "UPDATE live_events SET acknowledged=1 WHERE id=$1", id));
        hub.Publish("ack", new IdResponse(id));
        return Task.FromResult(new IdResponse(id));
    }

    public Task<OkResponse> AcknowledgeAllAsync()
    {
        Db.Write((c, tx) => Db.Exec(c, tx, "UPDATE live_events SET acknowledged=1 WHERE kind='notification'"));
        hub.Publish("ack", new AckAllMessage(true));
        return Task.FromResult(new OkResponse(true));
    }

    public Task<OkResponse> ClearAsync()
    {
        live.TurnOff();
        Db.Write((c, tx) => Db.Exec(c, tx, "DELETE FROM live_events"));
        hub.Publish("cleared", new OkResponse(true));
        return Task.FromResult(new OkResponse(true));
    }

    public async Task StreamAsync(Func<string, Task> write, bool once, CancellationToken ct)
    {
        var (reader, dispose) = hub.Subscribe();
        try
        {
            await write($"retry: 3000\nevent: state\ndata: {JsonSerializer.Serialize(live.Sim.State(), LiveHub.Json)}\n\n");
            if (once) return;
            while (!ct.IsCancellationRequested)
            {
                var wait = reader.WaitToReadAsync(ct).AsTask();
                if (await Task.WhenAny(wait, Task.Delay(15000, ct)) != wait) { await write(": keep-alive\n\n"); continue; }
                if (!await wait) break;
                var batch = new System.Text.StringBuilder();
                while (reader.TryRead(out var msg)) batch.Append(msg);
                await write(batch.ToString());
            }
        }
        catch (OperationCanceledException) { }
        finally { dispose(); }
    }

    public Task<ServiceResult<SensorReadingResponse>> SensorReadingAsync(JsonElement body)
    {
        var code = body.RawStr("vessel");
        if (code == null || body.Num("dip_cm") is not { } dip) return Task.FromResult(ServiceResult<SensorReadingResponse>.BadRequest("vessel and dip_cm are required"));
        LiveEvent? ev = null;
        var res = Db.Write((c, tx) =>
        {
            var v = Db.VesselRow(c, code, null);
            if (v == null) return ServiceResult<SensorReadingResponse>.NotFound("vessel not found");
            var m = Calibration.TryParse(v["calibration_json"] as string);
            if (m == null) return ServiceResult<SensorReadingResponse>.BadRequest("Volume unknown — enter litres manually");
            var l = Math.Round(m.LitresAtDip(dip), 1);
            Db.Exec(c, tx, "INSERT INTO sensor_readings(vessel_id,dip_cm,litres,created_at) VALUES($1,$2,$3,$4)", v["id"], dip, l, DateTime.UtcNow.ToString("o"));
            ev = LiveSim.SensorEvent((long)v["id"]!, (string)v["code"]!, v["cellar_label"] as string, l);
            ev.Id = Db.Insert(c, tx, "INSERT INTO live_events(created_at,sim_time,vessel_id,vessel_code,vessel_name,kind,metric,value,unit,source) VALUES($1,$2,$3,$4,$5,'reading','level',$6,'L','SENSOR')",
                ev.CreatedAt, ev.SimTime, ev.VesselId, ev.VesselCode, ev.VesselName, l);
            return ServiceResult<SensorReadingResponse>.Ok(new SensorReadingResponse(code, dip, l, "Calculated"));
        });
        if (ev != null) hub.Publish("sensor", ev);
        return Task.FromResult(res);
    }
}
