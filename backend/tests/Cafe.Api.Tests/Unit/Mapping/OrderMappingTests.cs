using Cafe.Api.Models.Entities;
using Cafe.Api.Models.Enums;
using Cafe.Api.Services.Orders;
using Xunit;

namespace Cafe.Api.Tests.Unit.Mapping;

public class OrderMappingTests
{
    private static Order Sample(PaymentMethod method, PaymentStatus status) => new()
    {
        Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        CustomerName = "Juan",
        CustomerEmail = "juan@example.com",
        CustomerPhone = "09171234567",
        PickupTime = new DateTime(2026, 10, 10, 6, 0, 0, DateTimeKind.Utc),
        TotalAmount = 430m,
        PaymentMethod = method,
        PaymentStatus = status,
        CheckoutUrl = "https://checkout.paymongo.com/cs_1",
        // Deliberately out of order: the DTO must sort by item id
        Items =
        {
            new OrderItem { Id = 2, ProductVariantId = 20, ProductName = "Latte", VariantName = "Large", Quantity = 1, PriceAtPurchase = 170m },
            new OrderItem { Id = 1, ProductVariantId = 10, ProductName = "Flat White", VariantName = "Small", Quantity = 2, PriceAtPurchase = 130m }
        }
    };

    [Fact]
    public void Items_are_sorted_and_line_totals_are_price_times_quantity()
    {
        var dto = Sample(PaymentMethod.PayAtCounter, PaymentStatus.Unpaid).ToDto();

        Assert.Equal(["Flat White", "Latte"], dto.Items.Select(i => i.ProductName));
        Assert.Equal(130m, dto.Items[0].UnitPrice);
        Assert.Equal(260m, dto.Items[0].LineTotal);
        Assert.Equal(170m, dto.Items[1].LineTotal);
    }

    [Theory]
    [InlineData(PaymentMethod.Online, PaymentStatus.Unpaid, true)]
    [InlineData(PaymentMethod.Online, PaymentStatus.Paid, false)]
    [InlineData(PaymentMethod.Online, PaymentStatus.Expired, false)]
    [InlineData(PaymentMethod.PayAtCounter, PaymentStatus.Unpaid, false)]
    public void The_checkout_link_is_only_shown_while_an_online_order_is_waiting_for_payment(
        PaymentMethod method, PaymentStatus status, bool shown)
    {
        var dto = Sample(method, status).ToDto();

        Assert.Equal(shown, dto.CheckoutUrl is not null);
    }

    [Fact]
    public void Contact_details_and_totals_are_copied_through()
    {
        var dto = Sample(PaymentMethod.PayAtCounter, PaymentStatus.Unpaid).ToDto();

        Assert.Equal("Juan", dto.CustomerName);
        Assert.Equal("09171234567", dto.CustomerPhone);
        Assert.Equal(430m, dto.TotalAmount);
        Assert.Equal(new DateTime(2026, 10, 10, 6, 0, 0, DateTimeKind.Utc), dto.PickupTime);
    }
}