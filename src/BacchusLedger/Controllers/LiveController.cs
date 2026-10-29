using System.Text.Json;
using BacchusLedger.Contracts.Common;
using BacchusLedger.Contracts.Live;
using BacchusLedger.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Controllers;

[ApiController]
[Route("api")]
public sealed class LiveController(ILiveBoardService live) : ControllerBase
{
    [HttpGet("live/state")]
    public ActionResult<LiveStateDto> State() => Ok(live.State());

    [HttpPost("live/on")]
    public ActionResult<LiveStateDto> On() => Ok(live.TurnOn());

    [HttpPost("live/off")]
    public ActionResult<LiveStateDto> Off() => Ok(live.TurnOff());

    [HttpPost("live/scenario/{name}")]
    public ActionResult<LiveStateDto> Scenario(string name) => this.ToActionResult(live.SetScenario(name));

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
