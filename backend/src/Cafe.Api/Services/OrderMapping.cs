using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Entities;
using Cafe.Api.Models.Enums;
namespace Cafe.Api.Services;

internal static class OrderMapping
{
    public static OrderDto ToDto(this Order o) => new(
        o.Id, o.Status, o.CustomerName, o.CustomerEmail, o.CustomerPhone,
        o.PickupTime, o.TotalAmount, o.CreatedAt,
        o.PaymentMethod, o.PaymentStatus,
        o.PaymentMethod == PaymentMethod.Online && o.PaymentStatus == PaymentStatus.Unpaid ? o.CheckoutUrl : null,
        o.PaidAt,
        o.Items
            .OrderBy(i => i.Id)
            .Select(i => new OrderItemDto(
                i.ProductVariantId, i.ProductName, i.VariantName,
                i.Quantity, i.PriceAtPurchase, i.PriceAtPurchase * i.Quantity))
            .ToList());
}