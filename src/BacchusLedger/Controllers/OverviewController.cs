using BacchusLedger.Contracts.Overview;
using BacchusLedger.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Controllers;

[ApiController]
[Route("api")]
public sealed class OverviewController(IOverviewService overview) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<OverviewResponse>> Get() => Ok(await overview.GetAsync());

    [HttpGet("wineries")]
    public async Task<ActionResult<IReadOnlyList<WineryDto>>> Wineries() => Ok(await overview.GetWineriesAsync());
}
