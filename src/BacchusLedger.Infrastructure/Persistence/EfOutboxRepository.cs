using BacchusLedger.Application.Contracts.Outbox;
using BacchusLedger.Application.Messaging;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class EfOutboxRepository(BacchusLedgerDbContext db) : IOutboxRepository
{
    public Task<List<OutboxMessage>> ListPendingAsync(int limit) =>
        db.OutboxMessages.AsNoTracking().Where(m => m.Status == OutboxStatus.Pending).OrderBy(m => m.OccurredAt).ThenBy(m => m.Seq).Take(limit)
            .Select(m => new OutboxMessage(m.Id, m.Type, m.Payload, m.OccurredAt, m.Vessel, m.Attempts, m.NextAttemptAt)).ToListAsync();

    public Task MarkProcessedAsync(Guid id, DateTime at) =>
        db.OutboxMessages.Where(m => m.Id == id)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, OutboxStatus.Processed).SetProperty(m => m.ProcessedAt, at)).GuardAsync();

    public Task MarkAttemptFailedAsync(Guid id, int attempts, DateTime nextAttemptAt, string error, bool deadLetter) =>
        db.OutboxMessages.Where(m => m.Id == id).ExecuteUpdateAsync(u => u
            .SetProperty(m => m.Attempts, attempts).SetProperty(m => m.NextAttemptAt, nextAttemptAt).SetProperty(m => m.LastError, error)
            .SetProperty(m => m.Status, deadLetter ? OutboxStatus.Failed : OutboxStatus.Pending)).GuardAsync();

    public Task<bool> HasHandledAsync(Guid messageId, string handler) =>
        db.ProcessedMessages.AnyAsync(p => p.MessageId == messageId && p.Handler == handler);

    public Task MarkHandledAsync(Guid messageId, string handler, DateTime at) =>
        db.InsertAsync(new ProcessedMessageEntity { MessageId = messageId, Handler = handler, ProcessedAt = at });

    public async Task<OutboxCounts> CountAsync()
    {
        var counts = await db.OutboxMessages.GroupBy(m => m.Status).Select(g => new { g.Key, Count = g.LongCount() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var oldest = await db.OutboxMessages.Where(m => m.Status == OutboxStatus.Pending).OrderBy(m => m.OccurredAt).Select(m => m.OccurredAt).Take(1).ToListAsync();
        return new OutboxCounts(counts.GetValueOrDefault(OutboxStatus.Pending), counts.GetValueOrDefault(OutboxStatus.Processed),
            counts.GetValueOrDefault(OutboxStatus.Failed), oldest.Count > 0 ? oldest[0] : null);
    }

    public Task<List<FailedMessageDto>> ListFailedAsync(int limit) =>
        db.OutboxMessages.AsNoTracking().Where(m => m.Status == OutboxStatus.Failed).OrderByDescending(m => m.NextAttemptAt).Take(limit)
            .Select(m => new FailedMessageDto(m.Id, m.Type, m.Vessel, m.OccurredAt, m.Attempts, m.LastError)).ToListAsync();

    public async Task<bool> RetryFailedAsync(Guid id, DateTime now) =>
        await db.OutboxMessages.Where(m => m.Id == id && m.Status == OutboxStatus.Failed).ExecuteUpdateAsync(u => u
            .SetProperty(m => m.Status, OutboxStatus.Pending).SetProperty(m => m.Attempts, 0).SetProperty(m => m.NextAttemptAt, now)).GuardAsync() > 0;

    public async Task<int> DeleteProcessedBeforeAsync(DateTime cutoff)
    {
        var messages = await db.OutboxMessages.Where(m => m.Status == OutboxStatus.Processed && m.ProcessedAt < cutoff).ExecuteDeleteAsync().GuardAsync();
        // a failed message keeps its handled marks, so a retry never runs a handler that already succeeded
        await db.ProcessedMessages.Where(p => p.ProcessedAt < cutoff && !db.OutboxMessages.Any(m => m.Id == p.MessageId)).ExecuteDeleteAsync().GuardAsync();
        return messages;
    }
}
