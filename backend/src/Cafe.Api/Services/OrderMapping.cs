using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Entities;
namespace Cafe.Api.Services;

internal static class OrderMapping
{
    public static OrderDto ToDto(this Order o) => new(
        o.Id, o.Status, o.CustomerName, o.CustomerEmail, o.CustomerPhone,
        o.PickupTime, o.TotalAmount, o.CreatedAt,
        o.Items
            .OrderBy(i => i.Id)
            .Select(i => new OrderItemDto(
                i.ProductVariantId, i.ProductName, i.VariantName,
                i.Quantity, i.PriceAtPurchase, i.PriceAtPurchase * i.Quantity))
            .ToList());
}