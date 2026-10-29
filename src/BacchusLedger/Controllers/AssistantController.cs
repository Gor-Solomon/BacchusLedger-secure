using System.Text.Json;
using BacchusLedger.Contracts.Assistant;
using BacchusLedger.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Controllers;

[ApiController]
[Route("api/assistant")]
public sealed class AssistantController(IAssistantService assistant) : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<AssistantStatusDto> Status(string? lang) => Ok(assistant.Status(lang));

    [HttpGet("quick/{n:int}")]
    public async Task<ActionResult<AssistantAnswer>> Quick(int n, string? lang) => this.ToActionResult(await assistant.QuickAsync(n, lang));

    [HttpPost("ask")]
    public async Task<ActionResult<AssistantAnswer>> Ask([FromBody] JsonElement body) =>
        Ok(await assistant.AskAsync(body, $"http://127.0.0.1:{HttpContext.Connection.LocalPort}"));
}
