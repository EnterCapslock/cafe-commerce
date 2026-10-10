using Cafe.Api.Models.Dtos;
namespace Cafe.Api.Services.Interfaces;

public sealed record CreateOrderResult(
    OrderDto? Order, IReadOnlyList<string> Errors, bool PaymentUnavailable = false)
{
    public bool Succeeded => Order is not null;
    public static CreateOrderResult Ok(OrderDto order) => new(order, []);
    public static CreateOrderResult Fail(IReadOnlyList<string> errors) => new(null, errors);
    public static CreateOrderResult GatewayDown() => new(null, [], true);
}

public interface IOrderService
{
    Task<CreateOrderResult> CreateAsync(CreateOrderRequest request, int? userId, CancellationToken ct);

    /// <returns>null if not found, or if the order belongs to someone else.</returns>
    Task<OrderDto?> GetByIdAsync(Guid id, int? requesterId, bool isAdmin, CancellationToken ct);

    Task<IReadOnlyList<OrderDto>> GetForUserAsync(int userId, CancellationToken ct);

    /// <returns>true if this call changed the order to Paid; false if ignored (unknown, mismatched, or already paid).</returns>
    Task<bool> MarkPaidAsync(Guid orderId, string checkoutSessionId, CancellationToken ct);
}