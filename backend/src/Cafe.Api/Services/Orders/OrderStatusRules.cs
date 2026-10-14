using Cafe.Api.Models.Enums;
namespace Cafe.Api.Services.Orders;

/// <summary>The only moves an order can make. Completed and Cancelled are final.</summary>
internal static class OrderStatusRules
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Preparing, OrderStatus.Cancelled],
        [OrderStatus.Preparing] = [OrderStatus.ReadyForPickup, OrderStatus.Cancelled],
        [OrderStatus.ReadyForPickup] = [OrderStatus.Completed, OrderStatus.Cancelled],
        [OrderStatus.Completed] = [],
        [OrderStatus.Cancelled] = []
    };

    public static IReadOnlyList<OrderStatus> NextOptions(OrderStatus from) => Allowed[from];

    public static bool CanMove(OrderStatus from, OrderStatus to) => Allowed[from].Contains(to);
}