using BacchusLedger.Application.Contracts.Live;
using BacchusLedger.Application.Contracts.Report;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class SqliteLiveEventRepository(ISqliteConnectionFactory connections) : ILiveEventRepository
{
    public Task<List<LiveEventRowDto>> ListAsync(string? kind, string? vesselCode, int limit) => Rows("""
        SELECT * FROM live_events WHERE ($1 IS NULL OR kind=$1) AND ($2 IS NULL OR vessel_code=$2) ORDER BY id DESC LIMIT $3
        """, kind, vesselCode, limit);

    public Task<List<LiveEventRowDto>> ListOpenAlertsAsync(int limit) =>
        Rows("SELECT * FROM live_events WHERE kind='notification' AND acknowledged=0 AND level IN ('red','amber') ORDER BY id DESC LIMIT $1", limit);

    public async Task<List<SimulatedAlertDto>> ListVesselNotificationsAsync(long vesselId)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("""
            SELECT created_at, sim_time, level, rule, message, scenario, source, acknowledged FROM live_events
            WHERE kind='notification' AND vessel_id=$1 ORDER BY id DESC LIMIT 50
            """, r => new SimulatedAlertDto(r.Str("created_at"), r.Str("sim_time"), r.Str("level"), r.Str("rule"), r.Str("message"), r.Str("scenario"), r.Str("source"),
            r.LongOrNull("acknowledged")), vesselId);
    }

    public Task<long> CountVesselReadingsAsync(long vesselId) => Count("SELECT COUNT(*) n FROM live_events WHERE kind='reading' AND vessel_id=$1", vesselId);

    public async Task<long> InsertAsync(LiveEvent e)
    {
        await using var db = await connections.OpenAsync();
        return await db.InsertAsync("""
            INSERT INTO live_events(created_at,sim_time,vessel_id,vessel_code,vessel_name,kind,metric,value,unit,level,rule,message,value_usd,scenario,source,acknowledged)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,0)
            """, e.CreatedAt, e.SimTime, e.VesselId, e.VesselCode, e.VesselName, e.Kind, e.Metric, e.Value, e.Unit, e.Level, e.Rule, e.Message, e.ValueUsd, e.Scenario, e.Source);
    }

    public Task AcknowledgeAsync(long id) => Exec("UPDATE live_events SET acknowledged=1 WHERE id=$1", id);

    public Task AcknowledgeAllNotificationsAsync() => Exec("UPDATE live_events SET acknowledged=1 WHERE kind='notification'");

    public Task DeleteAllAsync() => Exec("DELETE FROM live_events");

    public Task TrimReadingsAsync(int keep) => Exec("DELETE FROM live_events WHERE kind='reading' AND id < (SELECT COALESCE(MAX(id),0) - $1 FROM live_events)", keep);

    public Task<long> CountSimulatedAsync() => Count("SELECT COUNT(*) n FROM live_events WHERE source='SIMULATED'");

    public Task<long> CountUntaggedAsync() => Count("SELECT COUNT(*) n FROM live_events WHERE source NOT IN ('SIMULATED','SENSOR')");

    public Task InsertSensorReadingAsync(long vesselId, double dipCm, double litres, string createdAt) =>
        Exec("INSERT INTO sensor_readings(vessel_id,dip_cm,litres,created_at) VALUES($1,$2,$3,$4)", vesselId, dipCm, litres, createdAt);

    async Task<List<LiveEventRowDto>> Rows(string sql, params object?[] args)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync(sql, r => new LiveEventRowDto(r.Long("id"), r.Str("created_at"), r.Str("sim_time"), r.LongOrNull("vessel_id"), r.Str("vessel_code"),
            r.Str("vessel_name"), r.Str("kind"), r.Str("metric"), r.Dbl("value"), r.Str("unit"), r.Str("level"), r.Str("rule"), r.Str("message"), r.Dbl("value_usd"),
            r.Str("scenario"), r.Str("source"), r.LongOrNull("acknowledged")), args);
    }

    async Task<long> Count(string sql, params object?[] args)
    {
        await using var db = await connections.OpenAsync();
        return await db.CountAsync(sql, args);
    }

    async Task Exec(string sql, params object?[] args)
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync(sql, args);
    }
}
