using System.Net;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;
using Cafe.Api.Tests.TestSupport;
using Xunit;

namespace Cafe.Api.Tests.Integration;

public class AdminOrdersTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private HttpClient Client => api.Client;

    private async Task<OrderDto> CounterOrderAsync(string? name = null, string? email = null, string? phone = null)
    {
        var product = await api.CreateProductAsync([("Regular", 130m)]);
        return await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id, name: name, email: email, phone: phone));
    }

    private async Task<OrderDto> OnlineOrderAsync()
    {
        var product = await api.CreateProductAsync([("Regular", 130m)]);
        return await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id, method: PaymentMethod.Online));
    }

    [Fact]
    public async Task The_board_hides_unpaid_online_orders_until_they_are_paid()
    {
        var counter = await CounterOrderAsync();
        var online = await OnlineOrderAsync();

        var before = await api.GetBoardAsync();
        Assert.Contains(before, o => o.Id == counter.Id);
        Assert.DoesNotContain(before, o => o.Id == online.Id);

        await api.MarkPaidAsync(online);

        var after = await api.GetBoardAsync();
        Assert.Contains(after, o => o.Id == online.Id);
    }

    [Fact]
    public async Task Orders_follow_the_status_path_and_a_counter_order_is_paid_when_collected()
    {
        var order = await CounterOrderAsync();

        Assert.Equal(OrderStatus.Preparing, (await api.ChangeStatusAsync(order.Id, OrderStatus.Preparing)).Status);
        Assert.Equal(OrderStatus.ReadyForPickup, (await api.ChangeStatusAsync(order.Id, OrderStatus.ReadyForPickup)).Status);

        var done = await api.ChangeStatusAsync(order.Id, OrderStatus.Completed);
        Assert.Equal(PaymentStatus.Paid, done.PaymentStatus);   // the money was collected at the counter
        Assert.NotNull(done.PaidAt);

        Assert.DoesNotContain(await api.GetBoardAsync(), o => o.Id == order.Id);
        Assert.Contains(await api.GetBoardAsync("?status=Completed"), o => o.Id == order.Id);

        // The customer sees the same thing
        Assert.Equal(OrderStatus.Completed, (await api.GetOrderAsync(order.Id)).Status);
    }

    [Fact]
    public async Task Illegal_moves_are_refused()
    {
        var order = await CounterOrderAsync();

        Assert.Equal(HttpStatusCode.Conflict, (await api.TryChangeStatusAsync(order.Id, OrderStatus.Completed)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.TryChangeStatusAsync(order.Id, OrderStatus.ReadyForPickup)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.TryChangeStatusAsync(order.Id, OrderStatus.Pending)).StatusCode);

        await api.ChangeStatusAsync(order.Id, OrderStatus.Cancelled);
        Assert.Equal(HttpStatusCode.Conflict, (await api.TryChangeStatusAsync(order.Id, OrderStatus.Preparing)).StatusCode);

        // A status that doesn't exist is a bad request, and an unknown order is a 404
        var url = $"/api/admin/orders/{order.Id}/status";
        Assert.Equal(HttpStatusCode.BadRequest, (await api.AdminAsync(HttpMethod.Patch, url, new { status = "Teleported" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.AdminAsync(HttpMethod.Patch, url, new { status = 99 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.TryChangeStatusAsync(Guid.NewGuid(), OrderStatus.Preparing)).StatusCode);
    }

    [Fact]
    public async Task An_unpaid_online_order_cannot_advance_but_can_be_cancelled()
    {
        var order = await OnlineOrderAsync();

        var advance = await api.TryChangeStatusAsync(order.Id, OrderStatus.Preparing);
        var problem = await advance.ReadProblemAsync(HttpStatusCode.Conflict);
        Assert.Contains("hasn't been paid", problem.Title);

        var cancelled = await api.ChangeStatusAsync(order.Id, OrderStatus.Cancelled);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task Cancelling_a_paid_order_keeps_it_marked_paid_so_a_refund_can_be_issued()
    {
        var order = await OnlineOrderAsync();
        await api.MarkPaidAsync(order);

        var cancelled = await api.ChangeStatusAsync(order.Id, OrderStatus.Cancelled);

        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(PaymentStatus.Paid, cancelled.PaymentStatus);
    }

    [Fact]
    public async Task Two_baristas_making_the_same_move_at_once_produce_exactly_one_success()
    {
        var order = await CounterOrderAsync();
        var token = await api.AdminTokenAsync();

        Task<HttpResponseMessage> StartPreparing() => Client.Call(
            HttpMethod.Patch, $"/api/admin/orders/{order.Id}/status", new UpdateOrderStatusRequest(OrderStatus.Preparing), token);

        var results = await Task.WhenAll(StartPreparing(), StartPreparing());

        var codes = results.Select(r => r.StatusCode).OrderBy(c => (int)c).ToArray();
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, codes);
        Assert.Equal(OrderStatus.Preparing, (await api.GetOrderAsync(order.Id)).Status);
    }

    [Fact]
    public async Task Search_finds_orders_by_name_email_phone_and_order_code()
    {
        var name = TestData.Unique("Zelda");
        var email = $"zelda-{Guid.NewGuid():N}@example.com";
        var phone = TestData.UniquePhone();
        var order = await CounterOrderAsync(name, email, phone);
        await CounterOrderAsync();   // a second order that must not match

        async Task AssertFindsOnly(string query)
        {
            var found = await api.GetBoardAsync($"?search={Uri.EscapeDataString(query)}");
            Assert.Equal(order.Id, Assert.Single(found).Id);
        }

        await AssertFindsOnly(name);
        await AssertFindsOnly(name.ToLowerInvariant());                          // case-insensitive
        await AssertFindsOnly(email);
        await AssertFindsOnly(phone);
        await AssertFindsOnly(order.Id.ToString()[..8].ToUpperInvariant());      // the 8-character order code

        // Wildcard characters are searched for literally, not as patterns
        Assert.Empty(await api.GetBoardAsync("?search=%25"));
        Assert.Empty(await api.GetBoardAsync("?search=_"));
    }

    [Fact]
    public async Task History_lists_cancelled_orders_including_ones_that_never_reached_the_board()
    {
        var unpaidOnline = await OnlineOrderAsync();
        await api.ChangeStatusAsync(unpaidOnline.Id, OrderStatus.Cancelled);

        var cancelled = await api.GetBoardAsync("?status=Cancelled");

        Assert.Contains(cancelled, o => o.Id == unpaidOnline.Id);
    }
}