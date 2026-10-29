using System.Text.Json;
using BacchusLedger.Application.Contracts.Operations;
using BacchusLedger.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Api.Controllers;

[ApiController]
[Route("api/operations")]
public sealed class OperationsController(IOperationService operations) : ControllerBase
{
    [HttpPost("preview")]
    public async Task<ActionResult<OperationPreviewResponse>> Preview([FromBody] JsonElement body) => this.ToActionResult(await operations.PreviewAsync(body));

    [HttpPost]
    public async Task<ActionResult<OperationSavedResponse>> Record([FromBody] JsonElement body) => this.ToActionResult(await operations.RecordAsync(body));
}
