using BacchusLedger.Application.Contracts.Overview;
using BacchusLedger.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class OverviewController(IOverviewService overview) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<OverviewResponse>> Get() => Ok(await overview.GetAsync());

    [HttpGet("wineries")]
    public async Task<ActionResult<IReadOnlyList<WineryDto>>> Wineries() => Ok(await overview.GetWineriesAsync());
}
