using BacchusLedger.Contracts.Health;
using BacchusLedger.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(IHealthService health) : ControllerBase
{
    [HttpGet]
    public ActionResult<HealthResponse> Get() => Ok(health.Get());
}
