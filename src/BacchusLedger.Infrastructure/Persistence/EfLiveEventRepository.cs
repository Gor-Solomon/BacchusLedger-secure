using System.Linq.Expressions;
using BacchusLedger.Application.Contracts.Live;
using BacchusLedger.Application.Contracts.Report;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class EfLiveEventRepository(BacchusLedgerDbContext db) : ILiveEventRepository
{
    static readonly Expression<Func<LiveEventEntity, LiveEventRowDto>> ToRow = e => new LiveEventRowDto(e.Id, e.CreatedAt, e.SimTime, e.VesselId, e.VesselCode,
        e.VesselName, e.Kind, e.Metric, e.Value, e.Unit, e.Level, e.Rule, e.Message, e.ValueUsd, e.Scenario, e.Source, e.Acknowledged);

    public Task<List<LiveEventRowDto>> ListAsync(string? kind, string? vesselCode, int limit) =>
        db.LiveEvents.AsNoTracking().Where(e => (kind == null || e.Kind == kind) && (vesselCode == null || e.VesselCode == vesselCode))
            .OrderByDescending(e => e.Id).Take(limit).Select(ToRow).ToListAsync();

    public Task<List<LiveEventRowDto>> ListOpenAlertsAsync(int limit) =>
        db.LiveEvents.AsNoTracking().Where(e => e.Kind == "notification" && e.Acknowledged == 0 && (e.Level == "red" || e.Level == "amber"))
            .OrderByDescending(e => e.Id).Take(limit).Select(ToRow).ToListAsync();

    public Task<List<SimulatedAlertDto>> ListVesselNotificationsAsync(long vesselId) =>
        db.LiveEvents.AsNoTracking().Where(e => e.Kind == "notification" && e.VesselId == vesselId).OrderByDescending(e => e.Id).Take(50)
            .Select(e => new SimulatedAlertDto(e.CreatedAt, e.SimTime, e.Level, e.Rule, e.Message, e.Scenario, e.Source, e.Acknowledged)).ToListAsync();

    public Task<long> CountVesselReadingsAsync(long vesselId) => db.LiveEvents.LongCountAsync(e => e.Kind == "reading" && e.VesselId == vesselId);

    public async Task<long> InsertAsync(LiveEvent e) => (await db.InsertAsync(new LiveEventEntity
    {
        CreatedAt = e.CreatedAt, SimTime = e.SimTime, VesselId = e.VesselId, VesselCode = e.VesselCode, VesselName = e.VesselName, Kind = e.Kind, Metric = e.Metric,
        Value = e.Value, Unit = e.Unit, Level = e.Level, Rule = e.Rule, Message = e.Message, ValueUsd = e.ValueUsd, Scenario = e.Scenario, Source = e.Source,
        Acknowledged = 0,
    })).Id;

    public Task AcknowledgeAsync(long id) => db.LiveEvents.Where(e => e.Id == id).ExecuteUpdateAsync(u => u.SetProperty(e => e.Acknowledged, 1L)).GuardAsync();

    public Task AcknowledgeAllNotificationsAsync() =>
        db.LiveEvents.Where(e => e.Kind == "notification").ExecuteUpdateAsync(u => u.SetProperty(e => e.Acknowledged, 1L)).GuardAsync();

    public Task DeleteAllAsync() => db.LiveEvents.ExecuteDeleteAsync().GuardAsync();

    /// Keeps only the newest readings (ids within `keep` of the newest event); notifications stay.
    public async Task TrimReadingsAsync(int keep)
    {
        var newest = await db.LiveEvents.MaxAsync(e => (long?)e.Id) ?? 0;
        await db.LiveEvents.Where(e => e.Kind == "reading" && e.Id < newest - keep).ExecuteDeleteAsync().GuardAsync();
    }

    public Task<long> CountSimulatedAsync() => db.LiveEvents.LongCountAsync(e => e.Source == "SIMULATED");

    public Task<long> CountUntaggedAsync() => db.LiveEvents.LongCountAsync(e => e.Source != "SIMULATED" && e.Source != "SENSOR");

    public Task InsertSensorReadingAsync(long vesselId, double dipCm, double litres, string createdAt) =>
        db.InsertAsync(new SensorReadingEntity { VesselId = vesselId, DipCm = dipCm, Litres = litres, CreatedAt = createdAt });
}
