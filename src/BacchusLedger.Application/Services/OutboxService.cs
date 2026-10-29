using BacchusLedger.Application.Contracts.Outbox;
using BacchusLedger.Application.Messaging;
using BacchusLedger.Application.Repositories;

namespace BacchusLedger.Application.Services;

public sealed class OutboxService(IOutboxRepository outbox, IUnitOfWork unitOfWork, TimeProvider clock) : IOutboxService
{
    DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<OutboxStatsDto> StatsAsync()
    {
        var c = await outbox.CountAsync();
        double? age = c.OldestPendingAt is { } t ? Math.Round(Math.Max(0, (Now - t).TotalSeconds), 1) : null;
        return new OutboxStatsDto(c.Pending, c.Processed, c.Failed, age);
    }

    public async Task<IReadOnlyList<FailedMessageDto>> FailedAsync(int? limit) => await outbox.ListFailedAsync(Math.Clamp(limit ?? 20, 1, 200));

    public async Task<ServiceResult<OutboxRetryResponse>> RetryAsync(Guid id) =>
        await unitOfWork.WriteAsync(() => outbox.RetryFailedAsync(id, Now))
            ? new OutboxRetryResponse(id, OutboxStatus.Pending)
            : ServiceResult<OutboxRetryResponse>.NotFound("no failed message with this id");
}
