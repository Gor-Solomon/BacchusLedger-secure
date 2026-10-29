using System.Text.Json;
using BacchusLedger.Application.Contracts.Readings;
using BacchusLedger.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Api.Controllers;

[ApiController]
[Route("api/readings")]
public sealed class ReadingsController(IReadingService readings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ReadingListResponse>> List(long? batch, string? vessel, string? winery) =>
        this.ToActionResult(await readings.ListAsync(batch, vessel, winery));

    [HttpPost("preview")]
    public async Task<ActionResult<ReadingPreviewResponse>> Preview([FromBody] JsonElement body) => this.ToActionResult(await readings.PreviewAsync(body));

    [HttpPost]
    public async Task<ActionResult<ReadingsSavedResponse>> Save([FromBody] JsonElement body) => this.ToActionResult(await readings.SaveAsync(body));
}
