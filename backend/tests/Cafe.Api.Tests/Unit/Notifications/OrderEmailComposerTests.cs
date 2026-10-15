using Cafe.Api.Models.Entities;
using Cafe.Api.Models.Enums;
using Cafe.Api.Infrastructure.Mail;
using Cafe.Api.Services.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Net;
using Xunit;

namespace Cafe.Api.Tests.Unit.Notifications;

public class OrderEmailComposerTests
{
    private static readonly Guid Id = Guid.Parse("e18b681f-2201-403b-85ef-5f2a713e4678");

    private static OrderEmailComposer Composer(string site = "https://cafe.example") => new(
        Options.Create(new EmailOptions { FromName = "The Cafe" }),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = site }).Build());

    private static Order Sample(PaymentMethod method = PaymentMethod.PayAtCounter, int? userId = null, string name = "Juan Dela Cruz") => new()
    {
        Id = Id,
        UserId = userId,
        CustomerName = name,
        CustomerEmail = "juan@example.com",
        PickupTime = new DateTime(2026, 10, 10, 12, 5, 0, DateTimeKind.Utc),   // 8:05 PM in the Philippines
        TotalAmount = 430m,
        PaymentMethod = method,
        Items =
        {
            new OrderItem { Id = 1, ProductName = "Flat White", VariantName = "Small", Quantity = 2, PriceAtPurchase = 130m },
            new OrderItem { Id = 2, ProductName = "Latte", VariantName = "Large", Quantity = 1, PriceAtPurchase = 170m }
        }
    };

    [Fact]
    public void A_counter_order_gets_a_received_email_with_everything_the_customer_needs()
    {
        var email = Composer().Compose(Sample(), EmailKind.OrderReceived);

        Assert.Equal("juan@example.com", email.ToAddress);
        Assert.Equal("We got your order #E18B681F", email.Subject);
        foreach (var body in new[] { email.HtmlBody, email.TextBody })
        {
            Assert.Contains("E18B681F", body);
            Assert.Contains("Oct 10, 2026, 8:05 PM PHT", body);
            Assert.Contains("https://cafe.example/orders/e18b681f-2201-403b-85ef-5f2a713e4678", body);
            Assert.Contains("₱430", body);
            Assert.Contains("₱260", body);   // 2 x 130
            Assert.Contains("₱170", body);
            Assert.Contains("pay ₱430 at the counter", body, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains("2 × Flat White (Small)", WebUtility.HtmlDecode(email.HtmlBody));
        Assert.Contains("2 x Flat White (Small)", email.TextBody);
    }

    [Fact]
    public void A_paid_online_order_says_payment_was_received()
    {
        var email = Composer().Compose(Sample(PaymentMethod.Online), EmailKind.OrderReceived);

        Assert.Equal("Payment received for order #E18B681F", email.Subject);
        Assert.Contains("Paid online: ₱430", email.TextBody);
        Assert.DoesNotContain("at the counter", email.TextBody);
    }

    [Fact]
    public void The_ready_email_says_so_in_the_subject()
    {
        var email = Composer().Compose(Sample(), EmailKind.ReadyForPickup);

        Assert.Equal("Your order #E18B681F is ready for pickup", email.Subject);
        Assert.Contains("ready", email.HtmlBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Customer_supplied_text_cannot_inject_html()
    {
        var order = Sample(name: "Juan <script>alert(1)</script>");
        order.Items.First().ProductName = "Flat <img src=x onerror=alert(1)>";

        var email = Composer().Compose(order, EmailKind.OrderReceived);

        Assert.DoesNotContain("<script>", email.HtmlBody);
        Assert.DoesNotContain("<img", email.HtmlBody);
        Assert.Contains("&lt;script&gt;", email.HtmlBody);
    }

    [Fact]
    public void Guests_are_pointed_to_find_my_order_and_account_holders_to_sign_in()
    {
        var guest = Composer().Compose(Sample(userId: null), EmailKind.OrderReceived);
        var member = Composer().Compose(Sample(userId: 7), EmailKind.OrderReceived);

        Assert.Contains("https://cafe.example/find-order", guest.TextBody);
        Assert.Contains("Sign in", member.TextBody);
        Assert.DoesNotContain("find-order", member.TextBody);
    }

    [Theory]
    [InlineData("https://cafe.example/")]
    [InlineData("https://cafe.example")]
    public void A_trailing_slash_on_the_site_address_does_not_double_up(string site)
    {
        var email = Composer(site).Compose(Sample(), EmailKind.OrderReceived);

        Assert.Contains("https://cafe.example/orders/", email.TextBody);
        Assert.DoesNotContain("//orders", email.TextBody);
    }

    [Theory]
    [InlineData(99.5, "₱99.50")]
    [InlineData(1600, "₱1,600")]
    [InlineData(130, "₱130")]
    public void Prices_are_formatted_in_pesos(double amount, string expected)
    {
        var order = Sample();
        order.TotalAmount = (decimal)amount;

        Assert.Contains(expected, Composer().Compose(order, EmailKind.OrderReceived).TextBody);
    }
}