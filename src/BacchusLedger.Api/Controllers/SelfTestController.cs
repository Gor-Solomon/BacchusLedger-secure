using BacchusLedger.Application.Contracts.SelfTest;
using BacchusLedger.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Api.Controllers;

[ApiController]
[Route("api/selftest")]
public sealed class SelfTestController(ISelfTestService selfTest, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SelfTestResponse>> Run() =>
        Ok(await selfTest.RunAsync($"http://127.0.0.1:{HttpContext.Connection.LocalPort}", env.WebRootPath));
}
