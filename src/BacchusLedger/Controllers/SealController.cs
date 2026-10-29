using BacchusLedger.Contracts.Seal;
using BacchusLedger.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Controllers;

[ApiController]
[Route("api")]
public sealed class SealController(ISealService seals) : ControllerBase
{
    [HttpPost("seal")]
    public async Task<ActionResult<SealResponse>> Seal(string? batch) =>
        this.ToActionResult(await seals.SealAsync(batch, $"{Request.Scheme}://{Request.Host}"));

    [HttpGet("verify")]
    public async Task<ActionResult<VerifyResponse>> Verify(string? batch) => this.ToActionResult(await seals.VerifyAsync(batch));

    [HttpPost("demo/reset-seal")]
    public async Task<ActionResult<ResetSealResponse>> ResetDemo(string? batch) => this.ToActionResult(await seals.ResetDemoAsync(batch));

    [HttpPost("demo/tamper")]
    public async Task<ActionResult<TamperResponse>> TamperDemo(string? batch) => this.ToActionResult(await seals.TamperDemoAsync(batch));
}
