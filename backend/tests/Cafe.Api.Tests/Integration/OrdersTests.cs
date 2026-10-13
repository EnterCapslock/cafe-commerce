using System.Net;
using System.Text.Json;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;
using Cafe.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cafe.Api.Tests.Integration;

public class OrdersTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private HttpClient Client => api.Client;

    [Fact]
    public async Task A_guest_order_is_priced_by_the_server_and_ignores_prices_from_the_client()
    {
        var product = await api.CreateProductAsync([("Regular", 130m)]);

        // The request sneaks in a price of one centavo
        var body = new
        {
            customerName = "Guest Gary",
            customerEmail = "Gary@Example.com",
            customerPhone = "+63 917 123 4567",
            pickupTime = api.Time.GetUtcNow().AddHours(1),
            paymentMethod = "PayAtCounter",
            items = new[] { new { variantId = product.Variants[0].Id, quantity = 3, price = 0.01m } }
        };
        var order = await (await Client.Call(HttpMethod.Post, "/api/orders", body)).ReadAsync<OrderDto>(HttpStatusCode.Created);

        Assert.Equal(390m, order.TotalAmount);                    // 3 x 130, from the database
        Assert.Equal(130m, order.Items[0].UnitPrice);
        Assert.Equal(390m, order.Items[0].LineTotal);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(PaymentMethod.PayAtCounter, order.PaymentMethod);
        Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus);
        Assert.Null(order.CheckoutUrl);
        Assert.Equal("09171234567", order.CustomerPhone);         // stored in the 09... format
        Assert.Equal("gary@example.com", order.CustomerEmail);
    }

    [Fact]
    public async Task Order_items_keep_the_name_and_price_from_the_moment_of_purchase()
    {
        var product = await api.CreateProductAsync([("Regular", 100m)]);
        var originalName = product.Name;
        var order = await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id));

        // The admin renames the item and raises its price
        var edit = new ProductInput(
            TestData.Unique("Renamed"), product.Description, ProductCategory.Merchandise, true,
            [new VariantInput(product.Variants[0].Id, "Renamed size", 999m, true)]);
        await (await api.AdminAsync(HttpMethod.Put, $"/api/admin/products/{product.Id}", edit))
            .ExpectAsync(HttpStatusCode.OK);

        var later = await api.GetOrderAsync(order.Id);
        Assert.Equal(originalName, later.Items[0].ProductName);
        Assert.Equal("Regular", later.Items[0].VariantName);
        Assert.Equal(100m, later.Items[0].UnitPrice);
        Assert.Equal(100m, later.TotalAmount);
    }

    [Fact]
    public async Task A_sold_out_option_cannot_be_ordered()
    {
        var product = await api.CreateProductAsync([("Small", 50m), ("Large", 80m)]);
        var edit = new ProductInput(product.Name, product.Description, ProductCategory.Merchandise, true,
        [
            new VariantInput(product.Variants[0].Id, "Small", 50m, true),
            new VariantInput(product.Variants[1].Id, "Large", 80m, false)
        ]);
        await (await api.AdminAsync(HttpMethod.Put, $"/api/admin/products/{product.Id}", edit)).ExpectAsync(HttpStatusCode.OK);

        var response = await Client.Call(HttpMethod.Post, "/api/orders", api.BuildOrder(product.Variants[1].Id));

        var problem = await response.ReadValidationAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Contains("currently unavailable", Assert.Single(problem.Errors["items"]));

        // The other option still works
        await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id));
    }

    [Fact]
    public async Task Archived_and_missing_items_cannot_be_ordered()
    {
        var archived = await api.CreateProductAsync([("Regular", 50m)], isActive: false);

        var archivedResponse = await Client.Call(HttpMethod.Post, "/api/orders", api.BuildOrder(archived.Variants[0].Id));
        var missingResponse = await Client.Call(HttpMethod.Post, "/api/orders", api.BuildOrder(variantId: 999_999));

        Assert.NotEmpty((await archivedResponse.ReadValidationAsync(HttpStatusCode.UnprocessableEntity)).Errors["items"]);
        Assert.NotEmpty((await missingResponse.ReadValidationAsync(HttpStatusCode.UnprocessableEntity)).Errors["items"]);
    }

    [Fact]
    public async Task Paying_at_the_counter_is_limited_to_one_thousand_pesos()
    {
        var atLimit = await api.CreateProductAsync([("Regular", 1000m)]);
        var overLimit = await api.CreateProductAsync([("Regular", 1000.01m)]);

        await api.PlaceOrderAsync(api.BuildOrder(atLimit.Variants[0].Id));   // exactly ₱1,000 is allowed

        var refused = await Client.Call(HttpMethod.Post, "/api/orders", api.BuildOrder(overLimit.Variants[0].Id));
        var problem = await refused.ReadValidationAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Contains("can't be paid at the counter", Assert.Single(problem.Errors["items"]));

        // The same order is fine when paid online
        var online = await api.PlaceOrderAsync(api.BuildOrder(overLimit.Variants[0].Id, method: PaymentMethod.Online));
        Assert.Equal(PaymentMethod.Online, online.PaymentMethod);
    }

    [Fact]
    public async Task An_online_order_starts_a_payment_session_and_stays_unpaid()
    {
        var product = await api.CreateProductAsync([("Regular", 130m)]);

        var order = await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id, quantity: 2, method: PaymentMethod.Online));

        var sessionId = api.PayMongo.SessionFor(order.Id);
        Assert.Equal($"https://checkout.paymongo.test/{sessionId}", order.CheckoutUrl);
        Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus);

        // What was sent to PayMongo: authenticated, in centavos, and tied to this order
        Assert.Equal("Basic", api.PayMongo.LastAuthorization!.Scheme);
        using var sent = JsonDocument.Parse(api.PayMongo.LastBody!);
        var attributes = sent.RootElement.GetProperty("data").GetProperty("attributes");
        Assert.Equal(order.Id.ToString(), attributes.GetProperty("reference_number").GetString());
        Assert.Equal(13000, attributes.GetProperty("line_items")[0].GetProperty("amount").GetInt64());
        Assert.Equal(2, attributes.GetProperty("line_items")[0].GetProperty("quantity").GetInt32());

        var stored = await api.QueryDbAsync(db =>
            db.Orders.Where(o => o.Id == order.Id).Select(o => o.PaymentReference).SingleAsync());
        Assert.Equal(sessionId, stored);
    }

    [Fact]
    public async Task When_the_payment_provider_is_down_nothing_is_saved()
    {
        var product = await api.CreateProductAsync([("Regular", 130m)]);
        var name = TestData.Unique("Unlucky");

        api.PayMongo.Fail = true;
        try
        {
            var response = await Client.Call(HttpMethod.Post, "/api/orders",
                api.BuildOrder(product.Variants[0].Id, method: PaymentMethod.Online, name: name));
            var problem = await response.ReadProblemAsync(HttpStatusCode.BadGateway);
            Assert.Equal("Online payment is temporarily unavailable.", problem.Title);

            // Paying at the counter still works while the provider is down
            await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id));
        }
        finally
        {
            api.PayMongo.Fail = false;
        }

        var saved = await api.QueryDbAsync(db => db.Orders.AnyAsync(o => o.CustomerName == name));
        Assert.False(saved);
    }

    [Fact]
    public async Task Invalid_orders_are_rejected_with_field_errors()
    {
        var product = await api.CreateProductAsync([("Regular", 100m)]);
        var variantId = product.Variants[0].Id;

        var tooSoon = await Client.Call(HttpMethod.Post, "/api/orders", api.BuildOrder(variantId, pickupMinutes: 5));
        Assert.Contains("pickupTime", (await tooSoon.ReadValidationAsync()).Errors.Keys);

        var badPhone = await Client.Call(HttpMethod.Post, "/api/orders", api.BuildOrder(variantId, phone: "12345"));
        Assert.Contains("customerPhone", (await badPhone.ReadValidationAsync()).Errors.Keys);

        var empty = api.BuildOrder(variantId) with { Items = [] };
        var emptyResponse = await Client.Call(HttpMethod.Post, "/api/orders", empty);
        Assert.Contains("items", (await emptyResponse.ReadValidationAsync()).Errors.Keys);
    }

    [Fact]
    public async Task Admin_accounts_cannot_place_orders()
    {
        var product = await api.CreateProductAsync([("Regular", 100m)]);
        var request = api.BuildOrder(product.Variants[0].Id);

        var asAdmin = await Client.Call(HttpMethod.Post, "/api/orders", request, await api.AdminTokenAsync());
        var problem = await asAdmin.ReadProblemAsync(HttpStatusCode.Forbidden);
        Assert.Equal("Admin accounts can't place orders.", problem.Title);

        await api.PlaceOrderAsync(request);   // as a guest, the same request is fine
    }

    [Fact]
    public async Task Guest_orders_open_by_link_but_account_orders_are_private()
    {
        var owner = await api.RegisterCustomerAsync();
        var other = await api.RegisterCustomerAsync();
        var product = await api.CreateProductAsync([("Regular", 100m)]);

        var guestOrder = await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id));
        var accountOrder = await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id), owner.Token);

        async Task<HttpStatusCode> Open(Guid id, string? token) =>
            (await Client.Call(HttpMethod.Get, $"/api/orders/{id}", token: token)).StatusCode;

        // A guest order is readable by anyone who has its unguessable link
        Assert.Equal(HttpStatusCode.OK, await Open(guestOrder.Id, null));
        Assert.Equal(HttpStatusCode.OK, await Open(guestOrder.Id, other.Token));

        // An account order is visible to its owner and to admins, and looks like it doesn't exist to everyone else
        Assert.Equal(HttpStatusCode.OK, await Open(accountOrder.Id, owner.Token));
        Assert.Equal(HttpStatusCode.OK, await Open(accountOrder.Id, await api.AdminTokenAsync()));
        Assert.Equal(HttpStatusCode.NotFound, await Open(accountOrder.Id, null));
        Assert.Equal(HttpStatusCode.NotFound, await Open(accountOrder.Id, other.Token));

        Assert.Equal(HttpStatusCode.NotFound, await Open(Guid.NewGuid(), null));
    }

    [Fact]
    public async Task My_orders_lists_only_the_signed_in_customers_orders_newest_first()
    {
        var alice = await api.RegisterCustomerAsync();
        var bob = await api.RegisterCustomerAsync();
        var product = await api.CreateProductAsync([("Regular", 100m)]);

        var first = await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id), alice.Token);
        api.Time.Advance(TimeSpan.FromSeconds(2));
        var second = await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id), alice.Token);
        await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id), bob.Token);
        await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id));   // a guest order belongs to nobody

        var mine = await (await Client.Call(HttpMethod.Get, "/api/orders/mine", token: alice.Token)).ReadAsync<List<OrderDto>>();

        Assert.Equal([second.Id, first.Id], mine.Select(o => o.Id));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.Call(HttpMethod.Get, "/api/orders/mine")).StatusCode);
    }

    [Fact]
    public async Task A_valid_token_for_a_deleted_account_places_a_guest_order()
    {
        var (token, user) = await api.RegisterCustomerAsync();
        var product = await api.CreateProductAsync([("Regular", 100m)]);

        await api.RunDbAsync(async db =>
        {
            db.Users.Remove(await db.Users.SingleAsync(u => u.Id == user.Id));
            await db.SaveChangesAsync();
        });

        var order = await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id), token);

        // It became a guest order, so anyone with the link can read it
        Assert.Equal(HttpStatusCode.OK, (await Client.Call(HttpMethod.Get, $"/api/orders/{order.Id}")).StatusCode);
    }
}