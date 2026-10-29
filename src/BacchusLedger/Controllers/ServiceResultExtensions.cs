using BacchusLedger.Contracts;
using BacchusLedger.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacchusLedger.Controllers;

public static class ServiceResultExtensions
{
    /// 200 with the value, 404 (with { error } when there is a message) or 400 { error }.
    public static ActionResult ToActionResult<T>(this ControllerBase controller, ServiceResult<T> result) => result.Status switch
    {
        ServiceStatus.Ok => controller.Ok(result.Value),
        ServiceStatus.NotFound => result.Error == null ? controller.NotFound() : controller.NotFound(new ErrorResponse(result.Error)),
        _ => controller.BadRequest(new ErrorResponse(result.Error ?? "")),
    };
}
