using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;
using Cafe.Api.Tests.TestSupport;
using Xunit;

namespace Cafe.Api.Tests.Integration;

public class OrderCleanupTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private async Task<OrderDto> OrderAsync(PaymentMethod method, int pickupMinutes = 60)
    {
        var product = await api.CreateProductAsync([("Regular", 130m)]);
        return await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id, method: method, pickupMinutes: pickupMinutes));
    }

    [Fact]
    public async Task Unpaid_online_orders_expire_after_thirty_minutes()
    {
        var order = await OrderAsync(PaymentMethod.Online);

        api.Time.Advance(TimeSpan.FromMinutes(29));
        await api.RunCleanupAsync();
        var early = await api.GetOrderAsync(order.Id);
        Assert.Equal(OrderStatus.Pending, early.Status);
        Assert.Equal(PaymentStatus.Unpaid, early.PaymentStatus);

        api.Time.Advance(TimeSpan.FromMinutes(2));          // 31 minutes in total
        await api.RunCleanupAsync();
        var late = await api.GetOrderAsync(order.Id);
        Assert.Equal(OrderStatus.Cancelled, late.Status);
        Assert.Equal(PaymentStatus.Expired, late.PaymentStatus);
        Assert.Null(late.CheckoutUrl);

        // The admin can still find it in the cancelled history
        Assert.Contains(await api.GetBoardAsync("?status=Cancelled"), o => o.Id == order.Id);
    }

    [Fact]
    public async Task Paid_online_orders_are_never_expired_or_treated_as_no_shows()
    {
        var order = await OrderAsync(PaymentMethod.Online);
        await api.MarkPaidAsync(order);

        api.Time.Advance(TimeSpan.FromHours(3));   // well past both the payment window and the pickup time
        await api.RunCleanupAsync();

        var after = await api.GetOrderAsync(order.Id);
        Assert.Equal(OrderStatus.Pending, after.Status);
        Assert.Equal(PaymentStatus.Paid, after.PaymentStatus);
    }

    [Fact]
    public async Task Counter_orders_nobody_collects_are_cancelled_twenty_minutes_after_pickup_time()
    {
        var waiting = await OrderAsync(PaymentMethod.PayAtCounter, pickupMinutes: 30);
        var ready = await OrderAsync(PaymentMethod.PayAtCounter, pickupMinutes: 30);
        var collected = await OrderAsync(PaymentMethod.PayAtCounter, pickupMinutes: 30);

        await api.ChangeStatusAsync(ready.Id, OrderStatus.Preparing);
        await api.ChangeStatusAsync(ready.Id, OrderStatus.ReadyForPickup);
        await api.ChangeStatusAsync(collected.Id, OrderStatus.Preparing);
        await api.ChangeStatusAsync(collected.Id, OrderStatus.ReadyForPickup);
        await api.ChangeStatusAsync(collected.Id, OrderStatus.Completed);

        // 19 minutes after the pickup time: still within the grace period
        api.Time.Advance(TimeSpan.FromMinutes(49));
        await api.RunCleanupAsync();
        Assert.Equal(OrderStatus.Pending, (await api.GetOrderAsync(waiting.Id)).Status);
        Assert.Equal(OrderStatus.ReadyForPickup, (await api.GetOrderAsync(ready.Id)).Status);

        // 21 minutes after
        api.Time.Advance(TimeSpan.FromMinutes(2));
        await api.RunCleanupAsync();
        Assert.Equal(OrderStatus.Cancelled, (await api.GetOrderAsync(waiting.Id)).Status);
        Assert.Equal(OrderStatus.Cancelled, (await api.GetOrderAsync(ready.Id)).Status);

        // An order that was already collected is left alone
        var done = await api.GetOrderAsync(collected.Id);
        Assert.Equal(OrderStatus.Completed, done.Status);
        Assert.Equal(PaymentStatus.Paid, done.PaymentStatus);
    }
}