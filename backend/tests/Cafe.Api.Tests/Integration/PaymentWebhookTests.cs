using System.Net;
using System.Text.Json;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;
using Cafe.Api.Tests.TestSupport;
using Xunit;

namespace Cafe.Api.Tests.Integration;

public class PaymentWebhookTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private async Task<OrderDto> OnlineOrderAsync()
    {
        var product = await api.CreateProductAsync([("Regular", 130m)]);
        return await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id, method: PaymentMethod.Online));
    }

    private async Task AssertStillUnpaid(Guid orderId)
    {
        var order = await api.GetOrderAsync(orderId);
        Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus);
        Assert.Null(order.PaidAt);
    }

    [Fact]
    public async Task A_correctly_signed_payment_event_marks_the_order_paid()
    {
        var order = await OnlineOrderAsync();
        var body = Webhooks.PaidEvent(api.PayMongo.SessionFor(order.Id), order.Id.ToString());

        var response = await api.PostWebhookAsync(body, Webhooks.Sign(body));

        await response.ExpectAsync(HttpStatusCode.OK);
        using var reply = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(reply.RootElement.GetProperty("received").GetBoolean());

        var paid = await api.GetOrderAsync(order.Id);
        Assert.Equal(PaymentStatus.Paid, paid.PaymentStatus);
        Assert.NotNull(paid.PaidAt);
        Assert.Null(paid.CheckoutUrl);                       // nothing left to pay
        Assert.Equal(OrderStatus.Pending, paid.Status);      // paying doesn't move it along: the barista does
    }

    [Fact]
    public async Task Delivering_the_same_event_twice_changes_nothing()
    {
        var order = await OnlineOrderAsync();
        var body = Webhooks.PaidEvent(api.PayMongo.SessionFor(order.Id), order.Id.ToString());

        await (await api.PostWebhookAsync(body, Webhooks.Sign(body))).ExpectAsync(HttpStatusCode.OK);
        var afterFirst = await api.GetOrderAsync(order.Id);

        await (await api.PostWebhookAsync(body, Webhooks.Sign(body))).ExpectAsync(HttpStatusCode.OK);   // PayMongo retries
        var afterSecond = await api.GetOrderAsync(order.Id);

        Assert.Equal(PaymentStatus.Paid, afterSecond.PaymentStatus);
        Assert.Equal(afterFirst.PaidAt, afterSecond.PaidAt);       // the original payment time is kept
        Assert.Equal(afterFirst.UpdatedAt, afterSecond.UpdatedAt);
    }

    [Fact]
    public async Task A_bad_signature_is_rejected_and_nothing_changes()
    {
        var order = await OnlineOrderAsync();
        var body = Webhooks.PaidEvent(api.PayMongo.SessionFor(order.Id), order.Id.ToString());

        var wrongSecret = await api.PostWebhookAsync(body, Webhooks.Sign(body, secret: "someone-elses-secret"));
        var tampered = await api.PostWebhookAsync(body + " ", Webhooks.Sign(body));   // signed one body, sent another
        var missing = await api.PostWebhookAsync(body, null);
        var malformed = await api.PostWebhookAsync(body, "garbage");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongSecret.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, tampered.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, malformed.StatusCode);
        await AssertStillUnpaid(order.Id);
    }

    [Fact]
    public async Task Events_that_are_not_payments_are_acknowledged_and_ignored()
    {
        var order = await OnlineOrderAsync();
        var session = api.PayMongo.SessionFor(order.Id);

        // A different kind of event, a payload that isn't JSON, a reference that isn't an order id,
        // and an order that doesn't exist: each is acknowledged (so PayMongo stops retrying) and ignored.
        var bodies = new[]
        {
            Webhooks.PaidEvent(session, order.Id.ToString(), eventType: "payment.failed"),
            "this is not json",
            Webhooks.PaidEvent(session, "not-a-guid"),
            Webhooks.PaidEvent(session, Guid.NewGuid().ToString())
        };

        foreach (var body in bodies)
            await (await api.PostWebhookAsync(body, Webhooks.Sign(body))).ExpectAsync(HttpStatusCode.OK);

        await AssertStillUnpaid(order.Id);
    }

    [Fact]
    public async Task A_payment_for_a_different_session_cannot_mark_the_order_paid()
    {
        var order = await OnlineOrderAsync();
        var body = Webhooks.PaidEvent("cs_some_other_session", order.Id.ToString());

        await (await api.PostWebhookAsync(body, Webhooks.Sign(body))).ExpectAsync(HttpStatusCode.OK);

        await AssertStillUnpaid(order.Id);
    }

    [Fact]
    public async Task A_counter_order_cannot_be_marked_paid_by_a_payment_event()
    {
        var product = await api.CreateProductAsync([("Regular", 130m)]);
        var counter = await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id));
        var body = Webhooks.PaidEvent("cs_made_up", counter.Id.ToString());

        await (await api.PostWebhookAsync(body, Webhooks.Sign(body))).ExpectAsync(HttpStatusCode.OK);

        await AssertStillUnpaid(counter.Id);
    }

    [Fact]
    public async Task A_payment_that_arrives_after_cancellation_is_recorded_but_the_order_stays_cancelled()
    {
        var order = await OnlineOrderAsync();
        await api.ChangeStatusAsync(order.Id, OrderStatus.Cancelled);

        await api.MarkPaidAsync(order);

        var after = await api.GetOrderAsync(order.Id);
        Assert.Equal(PaymentStatus.Paid, after.PaymentStatus);    // the money arrived, so a refund is needed
        Assert.Equal(OrderStatus.Cancelled, after.Status);
    }
}