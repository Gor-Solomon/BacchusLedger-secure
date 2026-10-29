using BacchusLedger.Contracts.Cellar;
using BacchusLedger.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Controllers;

[ApiController]
[Route("api/cellar")]
public sealed class CellarController(ICellarService cellar) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CellarResponse>> Get(string? date) => Ok(await cellar.GetAsync(date));
}
