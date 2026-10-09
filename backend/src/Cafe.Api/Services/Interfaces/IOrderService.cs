using Cafe.Api.Models.Dtos;
namespace Cafe.Api.Services.Interfaces;

public sealed record CreateOrderResult(OrderDto? Order, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Order is not null;
    public static CreateOrderResult Ok(OrderDto order) => new(order, []);
    public static CreateOrderResult Fail(IReadOnlyList<string> errors) => new(null, errors);
}

public interface IOrderService
{
    Task<CreateOrderResult> CreateAsync(CreateOrderRequest request, int? userId, CancellationToken ct);

    /// <returns>null if not found, or if the order belongs to someone else.</returns>
    Task<OrderDto?> GetByIdAsync(Guid id, int? requesterId, bool isAdmin, CancellationToken ct);

    Task<IReadOnlyList<OrderDto>> GetForUserAsync(int userId, CancellationToken ct);
}