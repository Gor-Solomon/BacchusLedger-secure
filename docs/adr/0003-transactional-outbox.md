# ADR 0003: Transactional outbox for sensor and live events

- Status: accepted
- Date: 2026-10-29
- Related: [0001 modular monolith](0001-modular-monolith.md), [0002 EF Core](0002-ef-core.md)

## Problem

In a real cellar, sensor readings must never be lost, even if alert processing fails or the app restarts.

Before this change, `POST /api/sensors/reading` saved the reading and then pushed it to the live stream in the same request,
and the live simulator ran the alert rules and pushed the result in the same step as it made the readings. If the rules
or the push failed, the work was gone: nothing remembered that it still had to be done. A crash between "saved" and "processed"
lost the processing for good.

## Decision

Use a **transactional outbox** inside the same app (no message broker):

1. The producer saves the business data **and** an event row (`outbox_messages`) in **one database transaction**
   (`IOutbox.EnqueueAsync` refuses to run outside `IUnitOfWork.WriteAsync`). Both are saved, or neither.
   - `POST /api/sensors/reading` → `sensor_readings` + `live_events` + `SensorReadingReceived`.
   - Each live simulator step → the simulated `live_events` readings + one `LiveReadingSimulated` per vessel.
2. A background worker (`OutboxProcessor`, a hosted `BackgroundService`) picks up pending messages, oldest first, keeping the order
   per vessel, and hands them to `IMessagePublisher`. It polls every `Outbox:PollSeconds` (1 s) and is also woken right after a
   commit that added messages.
3. `InProcessMessagePublisher` → `IEventDispatcher` finds the handlers in DI (`IEventHandler<TEvent>`) and runs each one in its
   own transaction together with a `processed_messages` row, so **a handler never runs twice for the same message**.
   - `RangeRulesHandler`: the existing live alert rules → notifications in `live_events` + `RangeAlertRaised` (itself an outbox event).
   - `LiveStreamHandler`: pushes `sensor` and `tick` messages to the SSE hub, exactly as the UI already expects them.
4. A failed message is retried with exponential backoff (1 s, 2 s, 4 s, 8 s …). After `Outbox:MaxAttempts` (5) it becomes `failed`
   (the dead-letter). Every failure is logged. `POST /api/outbox/failed/{id}/retry` puts it back to pending.
5. A crash or restart leaves messages `pending`; they are processed after the next start. On shutdown the worker finishes the
   current message and stops.

This is event-driven design without a message broker. Business code only knows `IOutbox` and `IEventHandler<T>`.

## Why not Kafka (or another broker) now

- One small app, one SQLite file, offline in a cellar: a broker is one more server to run, secure, back up and monitor,
  and it would not work offline on a single laptop without more setup.
- The outbox gives the important guarantee (nothing is lost between "saved" and "processed") with the database we already have.
- Even with a broker, you still need an outbox: writing to the database and to Kafka are two systems, and without an outbox
  one of the two writes can fail after the other succeeded. The outbox is step one either way.

## How to move to a broker later

Only the publisher and the place where handlers run change; producers, events and handlers stay as they are.

1. Write `KafkaMessagePublisher : IMessagePublisher` (or Azure Service Bus / RabbitMQ) in Infrastructure: `PublishAsync` sends the
   message (`type` as a header, `payload` as the body, `vessel` as the partition key so the order per vessel is kept) and returns
   when the broker confirms. Register it instead of `InProcessMessagePublisher`. The `OutboxProcessor` stays the relay: it still
   marks the row processed only after the broker confirmed, and retries otherwise.
2. Run the handlers in a consumer (the same app or another service): a small hosted service reads from the topic and calls the same
   `IEventDispatcher.DispatchAsync`. `processed_messages` stays the idempotency check, because brokers deliver at least once.
3. `LiveStreamHandler` must run in the process that holds the SSE connections (or the hub moves to something shared, e.g. Redis).

## Trade-offs

- **Polling delay.** A message waits at most `PollSeconds` (1 s). In practice the worker is woken by the commit, so the live stream
  is as fast as before. Notifications now reach the screen as a separate `tick` message right after the readings that caused them.
- **SQLite = single worker.** SQLite has one writer at a time, and the order per vessel is kept by one worker processing
  messages one by one. More workers would need a lease/lock column (`SELECT … FOR UPDATE SKIP LOCKED` does not exist in SQLite).
  That is fine for one cellar; a bigger setup would move to PostgreSQL or a broker.
- **Order per vessel vs. throughput.** A vessel whose message is waiting for a retry blocks its later messages. With the demo
  setting `OUTBOX_FAIL_RATE=0.5` the backlog grows (the simulator adds messages faster than the retries finish); it drains as
  soon as the rate is back to 0.
- **At least once for side effects outside the database.** The SSE push and the simulator's in-memory rule state are not part of
  the transaction: if the commit of a handler fails after the push, the push can happen again. Database side effects
  (notifications, `RangeAlertRaised`) are exactly once per handler.
- **More rows.** About one outbox row per vessel every 3 s while Live is on. Processed rows are deleted after
  `Outbox:KeepProcessedHours` (24 h).
- **Not mixed with the record.** Outbox tables hold plumbing only; journals, losses and ROI never read them (like `live_events`).
