using BacchusLedger.Application.Events;
using BacchusLedger.Application.Messaging;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Application.Services;

namespace BacchusLedger.Application.Handlers;

/// Runs the live alert rules (ranges from config/ranges.json, level drop, stuck fermentation, missing Brix) on simulated readings.
/// Each notification is stored in live_events and raised as RangeAlertRaised, in the same transaction as the handled mark.
public sealed class RangeRulesHandler(ILiveControl live, ILiveEventRepository events, IOutbox outbox) : IEventHandler<LiveReadingSimulated>
{
    public async Task HandleAsync(LiveReadingSimulated e, CancellationToken ct)
    {
        foreach (var note in live.ApplyRules(e))
        {
            note.Id = await events.InsertAsync(note);
            await outbox.EnqueueAsync(RangeAlertRaised.For(note, DateTime.UtcNow));
        }
    }
}
