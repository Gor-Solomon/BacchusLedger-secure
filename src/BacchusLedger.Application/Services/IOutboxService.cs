using BacchusLedger.Application.Contracts.Outbox;

namespace BacchusLedger.Application.Services;

/// The event pipeline as the Expert view sees it: counters, the dead-letter, and putting a failed message back.
public interface IOutboxService
{
    Task<OutboxStatsDto> StatsAsync();
    Task<IReadOnlyList<FailedMessageDto>> FailedAsync(int? limit);
    Task<ServiceResult<OutboxRetryResponse>> RetryAsync(Guid id);
}
