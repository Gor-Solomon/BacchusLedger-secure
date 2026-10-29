using System.Data.Common;
using BacchusLedger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BacchusLedger.Infrastructure.Outbox;

/// After a commit that added outbox messages, wakes the outbox worker (so a reading reaches the live stream without waiting for the poll).
public sealed class OutboxCommitInterceptor(OutboxSignal signal) : DbTransactionInterceptor
{
    public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        Reset(eventData);
        return result;
    }

    public override ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        Reset(eventData);
        return ValueTask.FromResult(result);
    }

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => Committed(eventData);

    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Committed(eventData);
        return Task.CompletedTask;
    }

    static void Reset(TransactionEndEventData e)
    {
        if (e.Context is BacchusLedgerDbContext db) db.HasNewOutboxMessages = false;
    }

    void Committed(TransactionEndEventData e)
    {
        if (e.Context is not BacchusLedgerDbContext { HasNewOutboxMessages: true } db) return;
        db.HasNewOutboxMessages = false;
        signal.Notify();
    }
}
