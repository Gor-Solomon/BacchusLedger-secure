using System.Text.Json;
using BacchusLedger.Application.Contracts.Settings;
using BacchusLedger.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class SettingsController(ISettingsService settings) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<ActionResult<IReadOnlyDictionary<string, string>>> Get() => Ok(await settings.GetAsync());

    [HttpPost("settings")]
    public async Task<ActionResult<IReadOnlyDictionary<string, string>>> Save([FromBody] Dictionary<string, JsonElement> values) =>
        this.ToActionResult(await settings.SaveAsync(values));

    [HttpPost("reset")]
    public async Task<ActionResult<ResetResponse>> Reset() => Ok(await settings.ResetAsync());
}
