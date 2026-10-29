using BacchusLedger.Application.Contracts.Journals;
using BacchusLedger.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Api.Controllers;

[ApiController]
[Route("api/journals")]
public sealed class JournalsController(IJournalService journals) : ControllerBase
{
    [HttpGet("coverage")]
    public async Task<ActionResult<IReadOnlyList<JournalCoverageDto>>> Coverage(long? batch, string? winery) => Ok(await journals.CoverageAsync(batch, winery));

    [HttpGet("{n:int}")]
    public async Task<ActionResult<JournalDto>> Get(int n, long? batch, string? winery) => this.ToActionResult(await journals.GetAsync(n, batch, winery));

    [HttpGet("{n:int}.csv")]
    public async Task<ActionResult> Csv(int n, long? batch, string? winery)
    {
        var result = await journals.CsvAsync(n, batch, winery);
        if (result.Value is not { } csv) return this.ToActionResult(result);
        return File(csv.Content, "text/csv; charset=utf-8", csv.FileName);
    }
}
