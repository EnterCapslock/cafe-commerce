using Cafe.Api.Extensions;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;
using Cafe.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Cafe.Api.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Policy = Policies.AdminOnly)]
public class AdminOrdersController(IAdminOrderService orders) : ControllerBase
{
    /// <summary>
    /// The order board. Without a status it lists active orders (soonest pickup first); with Completed
    /// or Cancelled it lists history (newest first). Search matches name, email, phone, or order code.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> List(
        [FromQuery] OrderStatus? status,
        [FromQuery] string? search,
        CancellationToken ct,
        [FromQuery] int limit = 100)
        => Ok(await orders.ListAsync(status, search, limit, ct));

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> UpdateStatus(
        Guid id, UpdateOrderStatusRequest request, CancellationToken ct)
    {
        var result = await orders.UpdateStatusAsync(id, request.Status, ct);
        if (!result.Succeeded) return this.ToProblem(result);
        return Ok(result.Value);
    }
}