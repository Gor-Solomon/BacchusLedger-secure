using BacchusLedger.Contracts.Batches;
using BacchusLedger.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Controllers;

[ApiController]
[Route("api/batches")]
public sealed class BatchesController(IBatchService batches) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BatchSummaryDto>>> List() => Ok(await batches.ListAsync());

    [HttpGet("{id:long}")]
    public async Task<ActionResult<BatchDetailResponse>> Get(long id) => this.ToActionResult(await batches.GetAsync(id));
}
