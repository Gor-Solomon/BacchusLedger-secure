using BacchusLedger.Application.Contracts.Outbox;
using BacchusLedger.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Api.Controllers;

/// The event pipeline (transactional outbox), shown on the Live board in Expert mode.
[ApiController]
[Route("api/outbox")]
public sealed class OutboxController(IOutboxService outbox) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<OutboxStatsDto>> Stats() => Ok(await outbox.StatsAsync());

    [HttpGet("failed")]
    public async Task<ActionResult<IReadOnlyList<FailedMessageDto>>> Failed(int? limit) => Ok(await outbox.FailedAsync(limit));

    [HttpPost("failed/{id:guid}/retry")]
    public async Task<ActionResult<OutboxRetryResponse>> Retry(Guid id) => this.ToActionResult(await outbox.RetryAsync(id));
}
