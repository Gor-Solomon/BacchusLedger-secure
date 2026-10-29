using System.Text.Json;
using BacchusLedger.Application.Contracts.Common;
using BacchusLedger.Application.Contracts.Vessels;
using BacchusLedger.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Api.Controllers;

[ApiController]
[Route("api/vessels")]
public sealed class VesselsController(IVesselService vessels) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VesselDto>>> List() => Ok(await vessels.ListAsync());

    [HttpGet("{code}")]
    public async Task<ActionResult<VesselDetailResponse>> Get(string code, string? winery) => this.ToActionResult(await vessels.GetAsync(code, winery));

    [HttpGet("{code}/litres")]
    public async Task<ActionResult> Litres(string code, double? dip, double? litres, string? winery) =>
        this.ToActionResult(await vessels.ConvertAsync(code, dip, litres, winery));

    [HttpPost]
    public async Task<ActionResult<VesselCreatedResponse>> Create([FromBody] JsonElement body) => this.ToActionResult(await vessels.CreateAsync(body));

    [HttpPost("{code}/details")]
    public async Task<ActionResult<OkResponse>> Details(string code, string? winery, [FromBody] JsonElement body) =>
        this.ToActionResult(await vessels.UpdateDetailsAsync(code, winery, body));
}
