using System.Security.Claims;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Cafe.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController(IOrderService orders, IOrderLookupService lookup) : ControllerBase
{
    /// <summary>Places an order as a guest, or as the logged-in user if a token is supplied.</summary>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("orders")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request, CancellationToken ct)
    {
        if (User.IsInRole("Admin"))
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Admin accounts can't place orders.",
                detail: "Please sign in with a customer account to place an order.");
        }

        var result = await orders.CreateAsync(request, CurrentUserId(), ct);

        if (result.PaymentUnavailable)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Online payment is temporarily unavailable.",
                detail: "Please try again in a moment.");
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

    /// <summary>Finds a guest order from the code on its page and the email used at checkout.</summary>
    [HttpPost("lookup")]
    [AllowAnonymous]
    [EnableRateLimiting("lookup")]
    [ProducesResponseType<OrderLookupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<OrderLookupResponse>> Lookup(LookupOrderRequest request, CancellationToken ct)
    {
        var result = await lookup.FindAsync(request.Code, request.Email, ct);

        if (result.Status == LookupStatus.Found)
            return Ok(new OrderLookupResponse(result.OrderId!.Value));

        if (result.Status == LookupStatus.TooManyAttempts)
            return Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Too many attempts. Please wait a while and try again.");

        // Every kind of miss gets this exact answer, so the endpoint reveals nothing about which orders exist.
        return Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "We couldn't find an order with that code and email.",
            detail: "Check both, or sign in if you ordered with an account.");
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