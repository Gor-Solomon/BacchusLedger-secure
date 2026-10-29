using System.Text.Json;
using BacchusLedger.Contracts.Common;
using BacchusLedger.Contracts.Origins;
using BacchusLedger.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Controllers;

[ApiController]
[Route("api")]
public sealed class OriginsController(IOriginService origins) : ControllerBase
{
    [HttpGet("farmers")]
    public async Task<ActionResult<IReadOnlyList<FarmerDto>>> Farmers() => Ok(await origins.ListFarmersAsync());

    [HttpPost("farmers")]
    public async Task<ActionResult<IdResponse>> AddFarmer([FromBody] JsonElement body) => this.ToActionResult(await origins.AddFarmerAsync(body));

    [HttpGet("plots")]
    public async Task<ActionResult<IReadOnlyList<PlotDto>>> Plots() => Ok(await origins.ListPlotsAsync());

    [HttpPost("plots")]
    public async Task<ActionResult<IdResponse>> AddPlot([FromBody] JsonElement body) => this.ToActionResult(await origins.AddPlotAsync(body));

    [HttpGet("deliveries")]
    public async Task<ActionResult<IReadOnlyList<DeliveryDto>>> Deliveries() => Ok(await origins.ListDeliveriesAsync());

    [HttpPost("deliveries")]
    public async Task<ActionResult<IdResponse>> AddDelivery([FromBody] JsonElement body) => this.ToActionResult(await origins.AddDeliveryAsync(body));
}
