# Review: event pipeline with a transactional outbox (step 8)

This page explains what changed in the branch `feature/outbox`, for a reviewer who did not watch the work.

**In one sentence:** a sensor reading (or a simulated live reading) and its event are now saved in one transaction, and a background
worker handles the event later, with retries, a dead-letter and no double work. The app looks and answers the same as before.

- Nothing was pushed. `main` was not changed. All work is in local commits on `feature/outbox` (branched from `main`).
- Why, and how a broker could plug in later: [docs/adr/0003-transactional-outbox.md](../adr/0003-transactional-outbox.md).

## What was added, and where

| Part | Where | What it does |
|---|---|---|
| Events | `Application/Events/` | `SensorReadingReceived`, `LiveReadingSimulated`, `RangeAlertRaised`: event id (Guid), occurred_at, vessel, payload. |
| Outbox | `Application/Messaging/IOutbox`, `Infrastructure/Persistence/EfOutbox` | Saves an event on the same DbContext, so in the same transaction. Refuses to run without a transaction. |
| Tables | migration `AddOutbox` | `outbox_messages` (id, type, payload JSON, occurred_at, vessel, seq, status, attempts, next_attempt_at, last_error, processed_at; index on status + next_attempt_at) and `processed_messages` (message_id + handler). |
| Producer | `LiveBoardService.SensorReadingAsync`, `LiveSimulatorHost.TickAsync` | Reading + event in one transaction. The simulator no longer runs the rules or pushes SSE itself. |
| Worker | `Infrastructure/Outbox/OutboxProcessor` (hosted service) → `Application/Messaging/OutboxBatchProcessor` | Polls every 1 s (woken at once by a commit), a new DI scope per round, oldest first, order per vessel, backoff, dead-letter, graceful stop. |
| Dispatcher | `EventDispatcher`, `OutboxEventTypes` | Finds `IEventHandler<TEvent>` in DI; each handler runs in its own transaction with its `processed_messages` row. |
| Handlers | `Application/Handlers/` | `RangeRulesHandler` (today's alert rules on simulated readings, range rules on sensor readings → notifications + `RangeAlertRaised`), `LiveStreamHandler` (SSE `sensor` / `tick`). |
| Publisher | `IMessagePublisher`, `InProcessMessagePublisher` | The one place a broker would plug in. |
| Visibility | `OutboxController`, `OutboxService`, `live.js` | `GET /api/outbox/stats`, `GET /api/outbox/failed`, `POST /api/outbox/failed/{id}/retry`; "Event pipeline" card on the Live board, Expert mode only. |
| Demo switch | `OutboxOptions.FailRate` ← env `OUTBOX_FAIL_RATE` (docker-compose default 0) | Handlers fail at random before they run, to show retries and the dead-letter. |

## One code example per part

**Producer: both or neither** (`LiveBoardService.SensorReadingAsync`)

```csharp
var res = await unitOfWork.WriteAsync(async () =>
{
    ...
    await events.InsertSensorReadingAsync(v.Id, dip, l, DateTime.UtcNow.ToString("o"));
    ev.Id = await events.InsertAsync(ev);
    await outbox.EnqueueAsync(SensorReadingReceived.For(ev, DateTime.UtcNow)); // same transaction
    return ServiceResult<SensorReadingResponse>.Ok(new SensorReadingResponse(code, dip, l, "Calculated"));
});
```

**Storage** (`EfOutbox.EnqueueAsync`)

```csharp
if (db.Database.CurrentTransaction == null)
    throw new InvalidOperationException("IOutbox.EnqueueAsync must run inside IUnitOfWork.WriteAsync, in the same transaction as the data.");
```

**Worker: order per vessel** (`OutboxBatchProcessor.ProcessDueAsync`)

```csharp
if (m.Vessel != null && blocked.Contains(m.Vessel)) continue;
if (m.NextAttemptAt > Now) { if (m.Vessel != null) blocked.Add(m.Vessel); continue; }
if (!await DeliverAsync(m) && m.Vessel != null) blocked.Add(m.Vessel);
```

**Retries** (`RetryPolicy`)

```csharp
public TimeSpan DelayAfter(int attempts) =>
    TimeSpan.FromSeconds(Math.Min(options.Value.RetryBaseSeconds * Math.Pow(2, Math.Max(0, attempts - 1)), Longest.TotalSeconds));
public bool IsExhausted(int attempts) => attempts >= Math.Max(1, options.Value.MaxAttempts);
```

**Dispatcher: never twice** (`EventDispatcher.DispatchAsync`)

```csharp
await unitOfWork.WriteAsync(async () =>
{
    if (await outbox.HasHandledAsync(message.Id, handler.Name)) return 0; // done in an earlier attempt
    faults.BeforeHandle(handler.Name);
    await handler.RunAsync(ct);
    await outbox.MarkHandledAsync(message.Id, handler.Name, clock.GetUtcNow().UtcDateTime);
    return 0;
});
```

**Handler** (`LiveStreamHandler`)

```csharp
public Task HandleAsync(LiveReadingSimulated e, CancellationToken ct)
{
    hub.Publish("tick", new LiveTickMessage(live.State(), e.Payload.Readings, []));
    return Task.CompletedTask;
}
```

**Simulator** (`LiveSimulatorHost.TickAsync`)

```csharp
var tick = await sim.ProduceAsync();
foreach (var note in tick.Notes) await outbox.EnqueueAsync(RangeAlertRaised.For(note, DateTime.UtcNow));
foreach (var readings in tick.Readings) await outbox.EnqueueAsync(readings);
```

**Registration** (`Application/DependencyInjection.AddOutboxFeature`): the rules run before the live stream.

```csharp
.AddEventHandler<LiveReadingSimulated, RangeRulesHandler>()
.AddEventHandler<SensorReadingReceived, LiveStreamHandler>()
.AddEventHandler<LiveReadingSimulated, LiveStreamHandler>()
.AddEventHandler<RangeAlertRaised, LiveStreamHandler>();
```

## Test results

| Check | Result |
|---|---|
| `dotnet build BacchusLedger.sln` | 0 errors, 0 warnings |
| `dotnet test` | **109 / 109** passed (Domain 30, Infrastructure 14, Api 65), run 3 times |
| `scripts/check.ps1` | **48 / 48** endpoints identical to the baseline, selftest **81 / 81** |
| Docker (`docker compose up --build -d`, port 8081, existing volume `bacchus-secure-data`) | migration `AddOutbox` applied; overview, batch list and all **7 seals** (status and final hash) identical before and after; selftest 81 / 81 |
| Browser, Live on + "Simulate spike" | red alert card, feed entry, bell counter: same as before |
| Browser, `OUTBOX_FAIL_RATE=0.5` | retries in the log, failed messages in the Expert card and `/api/outbox/failed`; retry endpoint → pending. Back to 0: backlog drained, alerts normal |

New tests:
- Unit (`tests/BacchusLedger.Infrastructure.Tests/Outbox/OutboxTests.cs`, real SQLite file, a hand-set clock): dispatcher routing,
  backoff 1 / 2 / 4 / 8 / 16 s, dead-letter after 5 attempts (and retry), idempotency (dispatched twice → handler runs once),
  order per vessel, enqueue without a transaction is refused.
- Integration (`tests/BacchusLedger.Api.Tests/OutboxPipelineTests.cs`, real app on a real SQLite file):
  a) sensor readings above the range → reading + outbox row → processed → SSE `sensor`, and after 3 in a row a real red `level_high`
     notification (SENSOR) in live_events and on the stream, not repeated on the 4th; plus hot fermentation → `temp_high` through the worker;
  b) failing handler → reading kept, 5 attempts, failed; retry endpoint → processed, the other handler did not run again;
  c) crash safety: messages written with the worker removed are processed when the app starts again on the same file;
  d) the outbox row cannot be saved (duplicate key) → the reading and its live event are not saved either.

## Skipped, different from the request, or worth knowing

1. **Sensor readings now run the range rules (new, asked for by the owner after the first review).** `RangeRulesHandler` also handles
   `SensorReadingReceived`: the level from the dip is checked against config/ranges.json (3 in a row out of range → one red alert,
   green "back to normal" when back in range), tagged **SENSOR**. The alert state for sensors (`SensorRangeRules`) is kept apart
   from the simulator's, so real and simulated readings never count towards each other's alerts. With the default ranges
   (0 L – capacity) a dip cannot be out of range; a per-vessel override in ranges.json can make it so (test (a) does this).
   In the UI an alert tagged SENSOR shows a green **SENSOR** chip (toast, bell feed, notification center, Live board, Cellar and Overview feeds,
   ledger report); SIMULATED alerts keep the purple SIMULATED chip.
2. **One event per vessel per simulator step** (not one per tick), so the order per vessel means something. The UI therefore gets
   up to 3 `tick` messages per step instead of 1, and a notification arrives as its own `tick` right after the readings
   (same toast, bell and feed). The SSE message names and JSON shapes are unchanged.
3. **Extra columns:** `vessel` (needed for the order per vessel) and `seq` (insertion order, for events of the same moment).
4. **The rules moved out of the simulator step** into `LiveSimulator.ApplyRules`, which uses the time, scenario and database facts
   saved in the event (so a late retry sees the same inputs). The selftest still runs the rules at once (`TickAsync`) in its
   rolled-back probe; its 81 results are byte-for-byte the same as the baseline.
5. **Not transactional:** the SSE push and the simulator's in-memory alert state. A failure after them can repeat a push
   (at least once). The demo failure switch throws before a handler runs, so it never leaves half-done work.
6. **The selftest's "nothing was written" snapshot** now also skips `outbox_messages` and `processed_messages` (like `live_events`),
   because Live mode writes them.
7. **At `OUTBOX_FAIL_RATE=0.5` the backlog grows** while Live is on (a vessel waits for its retrying message, and new readings keep
   coming every 3 s). It drains when the rate is set back to 0. Demo only.
8. **Browser cache:** after the upgrade, an open browser kept the old `live.js` until a hard reload. Static files have no version
   in their names (unchanged from before); press Ctrl+F5 once after deploying.
9. **Cleanup:** processed messages older than 24 h are deleted every 10 minutes (not asked for, keeps the table small).
10. A small wake-up signal (EF Core commit interceptor) lets the worker start right after a commit, instead of waiting for the 1 s poll.
