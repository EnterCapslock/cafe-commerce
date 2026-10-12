using Cafe.Api.Services;
using Microsoft.AspNetCore.Mvc;
namespace Cafe.Api.Extensions;

public static class ServiceResultExtensions
{
    /// <summary>Maps a failed service result to the matching RFC 7807 response.</summary>
    public static ActionResult ToProblem<T>(this ControllerBase controller, ServiceResult<T> result) =>
        result.Status switch
        {
            ServiceStatus.NotFound => controller.NotFound(),
            ServiceStatus.Invalid => controller.Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Message),
            ServiceStatus.Conflict => controller.Problem(statusCode: StatusCodes.Status409Conflict, title: result.Message),
            ServiceStatus.Unavailable => controller.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: result.Message),
            _ => controller.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
}