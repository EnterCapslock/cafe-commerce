using Cafe.Api.Models.Enums;
namespace Cafe.Api.Models.Dtos;

public record OrderLineRequest(int VariantId, int Quantity);

public record CreateOrderRequest(
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    DateTimeOffset PickupTime,
    IReadOnlyList<OrderLineRequest> Items);

public record OrderItemDto(
    int VariantId,
    string ProductName,
    string VariantName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public record OrderDto(
    Guid Id,
    OrderStatus Status,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    DateTime PickupTime,
    decimal TotalAmount,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemDto> Items);