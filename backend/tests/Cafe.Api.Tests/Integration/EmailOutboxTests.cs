using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;
using Cafe.Api.Services.Interfaces;
using Cafe.Api.Services.Orders;
using Cafe.Api.Services.Notifications;
using Cafe.Api.Tests.TestSupport;
using Xunit;

namespace Cafe.Api.Tests.Integration;

public class EmailOutboxTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static string NewEmail() => $"mail-{Guid.NewGuid():N}@example.com";

    private IEnumerable<EmailMessage> SentTo(string email) => api.Emails.Sent.Where(m => m.ToAddress == email);

    private async Task<OrderDto> OrderAsync(PaymentMethod method, string email, string? name = null)
    {
        var product = await api.CreateProductAsync([("Regular", 130m)]);
        return await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id, method: method, email: email, name: name));
    }

    [Fact]
    public async Task A_counter_order_queues_a_confirmation_that_the_worker_then_sends_once()
    {
        var email = NewEmail();
        var order = await OrderAsync(PaymentMethod.PayAtCounter, email, name: "Juan <b>Cruz</b>");

        var queued = Assert.Single(await api.OutboxFor(order.Id));
        Assert.Equal(EmailKind.OrderReceived, queued.Kind);
        Assert.Equal(EmailStatus.Pending, queued.Status);
        Assert.Equal(email, queued.ToAddress);
        Assert.Empty(SentTo(email));                                  // nothing is sent while the request runs

        await api.RunEmailWorkerAsync();

        var message = Assert.Single(SentTo(email));
        Assert.Contains($"#{OrderCode.For(order.Id)}", message.Subject);
        Assert.Contains($"/orders/{order.Id}", message.HtmlBody);
        Assert.DoesNotContain("<b>Cruz</b>", message.HtmlBody);       // the customer's name is escaped
        Assert.Contains("&lt;b&gt;", message.HtmlBody);

        var sent = Assert.Single(await api.OutboxFor(order.Id));
        Assert.Equal(EmailStatus.Sent, sent.Status);
        Assert.NotNull(sent.SentAt);

        await api.RunEmailWorkerAsync();                              // running again sends nothing more
        Assert.Single(SentTo(email));
    }

    [Fact]
    public async Task An_online_order_is_confirmed_when_paid_and_a_repeated_payment_event_adds_nothing()
    {
        var email = NewEmail();
        var order = await OrderAsync(PaymentMethod.Online, email);

        Assert.Empty(await api.OutboxFor(order.Id));                  // nothing while the payment is pending

        await api.MarkPaidAsync(order);
        await api.MarkPaidAsync(order);                               // the provider retries delivery

        Assert.Single(await api.OutboxFor(order.Id));

        await api.RunEmailWorkerAsync();
        var message = Assert.Single(SentTo(email));
        Assert.StartsWith("Payment received", message.Subject);
    }

    [Fact]
    public async Task The_pickup_email_goes_out_only_when_the_order_is_ready()
    {
        var email = NewEmail();
        var order = await OrderAsync(PaymentMethod.PayAtCounter, email);
        await api.RunEmailWorkerAsync();

        await api.ChangeStatusAsync(order.Id, OrderStatus.Preparing);
        Assert.Single(await api.OutboxFor(order.Id));                 // still just the confirmation

        await api.ChangeStatusAsync(order.Id, OrderStatus.ReadyForPickup);
        var queued = await api.OutboxFor(order.Id);
        Assert.Equal([EmailKind.OrderReceived, EmailKind.ReadyForPickup], queued.Select(q => q.Kind));

        await api.RunEmailWorkerAsync();
        Assert.Contains("ready for pickup", SentTo(email).Last().Subject);

        await api.ChangeStatusAsync(order.Id, OrderStatus.Completed);
        Assert.Equal(2, (await api.OutboxFor(order.Id)).Count);       // completing sends nothing
    }

    [Fact]
    public async Task A_cancelled_order_gets_no_further_email()
    {
        var email = NewEmail();
        var order = await OrderAsync(PaymentMethod.PayAtCounter, email);

        await api.ChangeStatusAsync(order.Id, OrderStatus.Cancelled);

        Assert.Single(await api.OutboxFor(order.Id));                 // only the original confirmation
    }

    [Fact]
    public async Task A_payment_that_arrives_after_cancellation_is_not_confirmed_by_email()
    {
        var order = await OrderAsync(PaymentMethod.Online, NewEmail());
        await api.ChangeStatusAsync(order.Id, OrderStatus.Cancelled);

        await api.MarkPaidAsync(order);

        Assert.Empty(await api.OutboxFor(order.Id));                  // a refund is needed, not a confirmation
    }

    [Fact]
    public async Task A_temporary_provider_failure_is_retried_later_with_a_delay()
    {
        var email = NewEmail();
        api.Emails.FailNext(transient: true);
        var order = await OrderAsync(PaymentMethod.PayAtCounter, email);   // the order itself is never affected

        await api.RunEmailWorkerAsync();

        var waiting = Assert.Single(await api.OutboxFor(order.Id));
        Assert.Equal(EmailStatus.Pending, waiting.Status);
        Assert.Equal(1, waiting.Attempts);
        Assert.NotNull(waiting.LastError);
        Assert.True(waiting.NextAttemptAt > api.Time.GetUtcNow().UtcDateTime);

        await api.RunEmailWorkerAsync();                                   // not due yet
        Assert.Empty(SentTo(email));

        api.Time.Advance(TimeSpan.FromMinutes(2));
        await api.RunEmailWorkerAsync();

        Assert.Single(SentTo(email));
        Assert.Equal(EmailStatus.Sent, Assert.Single(await api.OutboxFor(order.Id)).Status);
    }

    [Fact]
    public async Task A_permanent_failure_is_not_retried()
    {
        var email = NewEmail();
        api.Emails.FailNext(transient: false);
        var order = await OrderAsync(PaymentMethod.PayAtCounter, email);

        await api.RunEmailWorkerAsync();

        var failed = Assert.Single(await api.OutboxFor(order.Id));
        Assert.Equal(EmailStatus.Failed, failed.Status);
        Assert.Equal(1, failed.Attempts);

        api.Time.Advance(TimeSpan.FromHours(8));
        await api.RunEmailWorkerAsync();
        Assert.Empty(SentTo(email));
    }

    [Fact]
    public async Task Retries_stop_after_the_last_delay_is_used_up()
    {
        var email = NewEmail();
        var attempts = EmailOutboxWorker.RetryDelays.Length + 1;
        api.Emails.FailNext(transient: true, times: attempts);
        var order = await OrderAsync(PaymentMethod.PayAtCounter, email);

        for (var i = 0; i < attempts; i++)
        {
            await api.RunEmailWorkerAsync();
            api.Time.Advance(TimeSpan.FromHours(7));       // longer than the longest delay
        }

        var item = Assert.Single(await api.OutboxFor(order.Id));
        Assert.Equal(EmailStatus.Failed, item.Status);
        Assert.Equal(attempts, item.Attempts);
        Assert.Empty(SentTo(email));
    }

    [Fact]
    public async Task Finished_rows_are_deleted_after_thirty_days()
    {
        var order = await OrderAsync(PaymentMethod.PayAtCounter, NewEmail());
        await api.RunEmailWorkerAsync();
        Assert.Single(await api.OutboxFor(order.Id));

        api.Time.Advance(TimeSpan.FromDays(31));
        await api.RunEmailWorkerAsync();

        Assert.Empty(await api.OutboxFor(order.Id));
    }
}