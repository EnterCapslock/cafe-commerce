using Cafe.Api.Models.Enums;
using Cafe.Api.Services;
using Xunit;
using static Cafe.Api.Models.Enums.OrderStatus;

namespace Cafe.Api.Tests.Unit.Orders;

public class OrderStatusRulesTests
{
    // The only moves that exist. Everything else, including staying put, must be refused.
    private static readonly HashSet<(OrderStatus From, OrderStatus To)> Legal =
    [
        (Pending, Preparing), (Pending, Cancelled),
        (Preparing, ReadyForPickup), (Preparing, Cancelled),
        (ReadyForPickup, Completed), (ReadyForPickup, Cancelled)
    ];

    public static TheoryData<OrderStatus, OrderStatus> AllPairs()
    {
        var data = new TheoryData<OrderStatus, OrderStatus>();
        foreach (var from in Enum.GetValues<OrderStatus>())
            foreach (var to in Enum.GetValues<OrderStatus>())
                data.Add(from, to);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void Only_the_documented_moves_are_allowed(OrderStatus from, OrderStatus to)
    {
        Assert.Equal(Legal.Contains((from, to)), OrderStatusRules.CanMove(from, to));
    }

    [Theory]
    [InlineData(Completed)]
    [InlineData(Cancelled)]
    public void Finished_orders_cannot_change(OrderStatus status)
    {
        Assert.Empty(OrderStatusRules.NextOptions(status));
    }

    [Theory]
    [InlineData(Pending)]
    [InlineData(Preparing)]
    [InlineData(ReadyForPickup)]
    public void Every_active_order_can_be_cancelled(OrderStatus status)
    {
        Assert.True(OrderStatusRules.CanMove(status, Cancelled));
    }

    [Fact]
    public void An_order_cannot_skip_a_step()
    {
        Assert.False(OrderStatusRules.CanMove(Pending, ReadyForPickup));
        Assert.False(OrderStatusRules.CanMove(Pending, Completed));
        Assert.False(OrderStatusRules.CanMove(Preparing, Completed));
    }
}