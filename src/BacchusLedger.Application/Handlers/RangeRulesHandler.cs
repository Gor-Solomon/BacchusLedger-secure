using BacchusLedger.Application.Events;
using BacchusLedger.Application.Messaging;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Application.Services;

namespace BacchusLedger.Application.Handlers;

/// Runs the live alert rules on new readings: on simulated readings all of them (ranges from config/ranges.json, level drop,
/// stuck fermentation, missing Brix), on real sensor readings the ranges (kept apart from the simulator's state).
/// Each notification is stored in live_events and raised as RangeAlertRaised, in the same transaction as the handled mark.
public sealed class RangeRulesHandler(ILiveControl live, ISensorRangeRules sensorRules, IVesselRepository vessels, ILiveEventRepository events, IOutbox outbox)
    : IEventHandler<LiveReadingSimulated>, IEventHandler<SensorReadingReceived>
{
    public Task HandleAsync(LiveReadingSimulated e, CancellationToken ct) => RaiseAsync(live.ApplyRules(e));

    public async Task HandleAsync(SensorReadingReceived e, CancellationToken ct)
    {
        if (e.Vessel == null || await vessels.FindAsync(e.Vessel, null) is not { } vessel) return;
        var batch = await vessels.LastRealBatchAsync(vessel.Id);
        await RaiseAsync(sensorRules.Check(e.Payload, vessel, batch?.Code));
    }

    async Task RaiseAsync(IReadOnlyList<LiveEvent> notes)
    {
        foreach (var note in notes)
        {
            note.Id = await events.InsertAsync(note);
            await outbox.EnqueueAsync(RangeAlertRaised.For(note, DateTime.UtcNow));
        }
    }
}
