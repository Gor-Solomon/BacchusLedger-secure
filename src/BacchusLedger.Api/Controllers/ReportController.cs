using BacchusLedger.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Api.Controllers;

[ApiController]
[Route("api/report")]
public sealed class ReportController(IReportService reports) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Get(string? batch, string? vessel) => this.ToActionResult(await reports.GetAsync(batch, vessel));
}
