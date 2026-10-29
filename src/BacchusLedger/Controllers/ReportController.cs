using BacchusLedger.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Controllers;

[ApiController]
[Route("api/report")]
public sealed class ReportController(IReportService reports) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Get(string? batch, string? vessel) => this.ToActionResult(await reports.GetAsync(batch, vessel));
}
