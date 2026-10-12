using Cafe.Api.Models.Enums;
namespace Cafe.Api.Models.Dtos;

public record OrderLineRequest(int VariantId, int Quantity);

public record CreateOrderRequest(
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    DateTimeOffset PickupTime,
    IReadOnlyList<OrderLineRequest> Items,
    PaymentMethod PaymentMethod);

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
    DateTime UpdatedAt,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    string? CheckoutUrl,      // only present while an online order is still unpaid
    DateTime? PaidAt,
    IReadOnlyList<OrderItemDto> Items);