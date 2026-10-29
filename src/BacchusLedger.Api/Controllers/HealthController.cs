using BacchusLedger.Application.Contracts.Health;
using BacchusLedger.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(IHealthService health) : ControllerBase
{
    [HttpGet]
    public ActionResult<HealthResponse> Get() => Ok(health.Get());
}
