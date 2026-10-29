using BacchusLedger.Application.Contracts.Demo;
using BacchusLedger.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class DemoController(IDemoService demo) : ControllerBase
{
    [HttpGet("demo/steps")]
    public async Task<ActionResult<DemoStepsDto>> Steps(string? script) => Ok(await demo.StepsAsync(script));

    [HttpPost("demo/step/{n:int}")]
    public async Task<ActionResult<DemoCardDto>> Step(int n, string? script) => this.ToActionResult(await demo.StepAsync(n, script));

    [HttpPost("demo/reset")]
    public async Task<ActionResult<DemoStepsDto>> Reset(string? script) => Ok(await demo.ResetAsync(script));

    [HttpPost("try/w03")]
    public async Task<ActionResult<PracticeCopyResponse>> PracticeCopy() => Ok(await demo.CreatePracticeCopyAsync());

    [HttpPost("demo/readings/start")]
    public async Task<ActionResult<LiveDemoStartResponse>> StartLiveReadings(string? vessel) => Ok(await demo.StartLiveReadingsAsync(vessel));
}
