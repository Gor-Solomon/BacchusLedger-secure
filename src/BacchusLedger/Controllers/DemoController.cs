using BacchusLedger.Contracts.Demo;
using BacchusLedger.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Controllers;

[ApiController]
[Route("api")]
public sealed class DemoController(IDemoService demo) : ControllerBase
{
    [HttpGet("demo/steps")]
    public ActionResult<DemoStepsDto> Steps(string? script) => Ok(demo.Steps(script));

    [HttpPost("demo/step/{n:int}")]
    public ActionResult<DemoCardDto> Step(int n, string? script) => this.ToActionResult(demo.Step(n, script));

    [HttpPost("demo/reset")]
    public ActionResult<DemoStepsDto> Reset(string? script) => Ok(demo.Reset(script));

    [HttpPost("try/w03")]
    public async Task<ActionResult<PracticeCopyResponse>> PracticeCopy() => Ok(await demo.CreatePracticeCopyAsync());

    [HttpPost("demo/readings/start")]
    public async Task<ActionResult<LiveDemoStartResponse>> StartLiveReadings(string? vessel) => Ok(await demo.StartLiveReadingsAsync(vessel));
}
