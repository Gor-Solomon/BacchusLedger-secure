using System.Text.Json;
using BacchusLedger.Application.Contracts.Common;
using BacchusLedger.Application.Contracts.Live;
using BacchusLedger.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class LiveController(ILiveBoardService live) : ControllerBase
{
    [HttpGet("live/state")]
    public ActionResult<LiveStateDto> State() => Ok(live.State());

    [HttpPost("live/on")]
    public async Task<ActionResult<LiveStateDto>> On() => Ok(await live.TurnOnAsync());

    [HttpPost("live/off")]
    public async Task<ActionResult<LiveStateDto>> Off() => Ok(await live.TurnOffAsync());

    [HttpPost("live/scenario/{name}")]
    public async Task<ActionResult<LiveStateDto>> Scenario(string name) => this.ToActionResult(await live.SetScenarioAsync(name));

    [HttpGet("live/events")]
    public async Task<ActionResult<IReadOnlyList<LiveEventRowDto>>> Events(string? kind, string? vessel, int? limit) => Ok(await live.EventsAsync(kind, vessel, limit));

    [HttpPost("live/events/{id:long}/ack")]
    public async Task<ActionResult<IdResponse>> Acknowledge(long id) => Ok(await live.AcknowledgeAsync(id));

    [HttpPost("live/ack-all")]
    public async Task<ActionResult<OkResponse>> AcknowledgeAll() => Ok(await live.AcknowledgeAllAsync());

    [HttpPost("live/clear")]
    public async Task<ActionResult<OkResponse>> Clear() => Ok(await live.ClearAsync());

    [HttpGet("live/stream")]
    public async Task Stream(bool? once)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        var ct = HttpContext.RequestAborted;
        await live.StreamAsync(async text => { await Response.WriteAsync(text, ct); await Response.Body.FlushAsync(ct); }, once == true, ct);
    }

    [HttpPost("sensors/reading")]
    public async Task<ActionResult<SensorReadingResponse>> SensorReading([FromBody] JsonElement body) => this.ToActionResult(await live.SensorReadingAsync(body));
}
