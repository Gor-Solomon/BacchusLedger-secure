using BacchusLedger.Application.Events;
using BacchusLedger.Application.Messaging;
using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

/// Writes events into outbox_messages on the same DbContext (so the same transaction) as the repositories.
public sealed class EfOutbox(BacchusLedgerDbContext db, OutboxEventTypes types) : IOutbox
{
    public async Task EnqueueAsync(IOutboxEvent e)
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("IOutbox.EnqueueAsync must run inside IUnitOfWork.WriteAsync, in the same transaction as the data.");
        // writes are serialised (SqliteWriteGate), so the next number is safe to take here
        var seq = (await db.OutboxMessages.MaxAsync(m => (long?)m.Seq) ?? 0) + 1;
        await db.InsertAsync(new OutboxMessageEntity
        {
            Id = e.EventId, Type = types.NameOf(e), Payload = types.Serialize(e), OccurredAt = e.OccurredAt, Vessel = e.Vessel, Seq = seq,
            Status = OutboxStatus.Pending, Attempts = 0, NextAttemptAt = e.OccurredAt,
        });
        db.HasNewOutboxMessages = true;
    }
}
