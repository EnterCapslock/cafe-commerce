using System.Security.Claims;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Cafe.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public class OrdersController(IOrderService orders) : ControllerBase
{
    /// <summary>Places an order as a guest, or as the logged-in user if a token is supplied.</summary>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("orders")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var result = await orders.CreateAsync(request, CurrentUserId(), ct);

        if (result.PaymentUnavailable)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Online payment is temporarily unavailable.",
                detail: "Please try again, or choose to pay at the counter.");
        }

        if (!result.Succeeded)
        {
            return UnprocessableEntity(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["items"] = [.. result.Errors] })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "This order can't be placed as requested."
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Order!.Id }, result.Order);
    }

    /// <summary>Gets an order by ID. Guest orders are readable by anyone holding the ID.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken ct)
    {
        var order = await orders.GetByIdAsync(id, CurrentUserId(), User.IsInRole("Admin"), ct);
        return order is null ? NotFound() : Ok(order);
    }

    /// <summary>Lists the signed-in customer's 50 most recent orders.</summary>
    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<OrderDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> Mine(CancellationToken ct)
    {
        var userId = CurrentUserId();
        return userId is null ? Unauthorized() : Ok(await orders.GetForUserAsync(userId.Value, ct));
    }

    private int? CurrentUserId() =>
        int.TryParse(User.FindFirstValue("sub"), out var id) ? id : null;
}