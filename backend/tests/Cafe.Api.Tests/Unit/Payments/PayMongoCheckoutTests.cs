using System.Net;
using System.Text;
using System.Text.Json;
using Cafe.Api.Models.Entities;
using Cafe.Api.Infrastructure.Payments;
using Cafe.Api.Services.Interfaces;
using Cafe.Api.Tests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cafe.Api.Tests.Unit.Payments;

public class PayMongoCheckoutTests
{
    private const string SecretKey = "sk_test_unit";
    private const string OkResponse =
        """{"data":{"id":"cs_abc","attributes":{"checkout_url":"https://checkout.paymongo.com/cs_abc"}}}""";

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static PayMongoGateway Gateway(
        StubHttpHandler handler, string secretKey = SecretKey, string[]? methods = null, string? frontend = "https://cafe.example")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(frontend is null ? [] : new Dictionary<string, string?> { ["Frontend:BaseUrl"] = frontend })
            .Build();

        return new PayMongoGateway(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.paymongo.com/") },
            Options.Create(new PayMongoOptions { SecretKey = secretKey, PaymentMethodTypes = methods ?? [] }),
            config,
            NullLogger<PayMongoGateway>.Instance);
    }

    private static Order OrderOf(params (string Product, string Variant, int Qty, decimal Price)[] lines)
    {
        var order = new Order { Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") };
        foreach (var (product, variant, qty, price) in lines)
        {
            order.Items.Add(new OrderItem { ProductName = product, VariantName = variant, Quantity = qty, PriceAtPurchase = price });
        }
        return order;
    }

    private static JsonElement Attributes(string body) =>
        JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("attributes").Clone();

    [Fact]
    public async Task It_sends_an_authenticated_request_and_returns_the_session()
    {
        var handler = new StubHttpHandler((_, _) => Json(HttpStatusCode.OK, OkResponse));

        var session = await Gateway(handler).CreateCheckoutAsync(OrderOf(("Flat White", "Large", 2, 170m)), default);

        Assert.Equal(new CheckoutSession("cs_abc", "https://checkout.paymongo.com/cs_abc"), session);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.paymongo.com/v2/checkout_sessions", handler.Request.RequestUri!.ToString());
        Assert.Equal("Basic", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes(SecretKey + ":")), handler.Request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Line_items_carry_the_order_prices_in_centavos()
    {
        var handler = new StubHttpHandler((_, _) => Json(HttpStatusCode.OK, OkResponse));
        var order = OrderOf(("Flat White", "Large", 2, 170m), ("House Blend Beans", "1kg", 1, 1600m));

        await Gateway(handler).CreateCheckoutAsync(order, default);

        var items = Attributes(handler.Body!).GetProperty("line_items");
        Assert.Equal(2, items.GetArrayLength());

        Assert.Equal("Flat White (Large)", items[0].GetProperty("name").GetString());
        Assert.Equal(17000, items[0].GetProperty("amount").GetInt64());
        Assert.Equal(2, items[0].GetProperty("quantity").GetInt32());
        Assert.Equal("PHP", items[0].GetProperty("currency").GetString());

        Assert.Equal(160000, items[1].GetProperty("amount").GetInt64());
    }

    [Theory]
    [InlineData("19.99", 1999)]       // a classic floating-point trap: 19.99 * 100 is 1998.9999 in a double
    [InlineData("0.01", 1)]
    [InlineData("120", 12000)]
    [InlineData("1234.56", 123456)]
    public async Task Amounts_are_exact_whole_centavos(string pesos, long centavos)
    {
        var handler = new StubHttpHandler((_, _) => Json(HttpStatusCode.OK, OkResponse));
        var order = OrderOf(("Item", "Regular", 1, decimal.Parse(pesos, System.Globalization.CultureInfo.InvariantCulture)));

        await Gateway(handler).CreateCheckoutAsync(order, default);

        Assert.Equal(centavos, Attributes(handler.Body!).GetProperty("line_items")[0].GetProperty("amount").GetInt64());
    }

    [Fact]
    public async Task The_order_id_is_the_reference_and_the_customer_returns_to_the_order_page()
    {
        var handler = new StubHttpHandler((_, _) => Json(HttpStatusCode.OK, OkResponse));
        var order = OrderOf(("Latte", "Small", 1, 130m));

        await Gateway(handler, frontend: "https://cafe.example/").CreateCheckoutAsync(order, default);

        var attributes = Attributes(handler.Body!);
        Assert.Equal(order.Id.ToString(), attributes.GetProperty("reference_number").GetString());
        Assert.Equal($"https://cafe.example/orders/{order.Id}", attributes.GetProperty("success_url").GetString());
        Assert.Equal($"https://cafe.example/orders/{order.Id}", attributes.GetProperty("cancel_url").GetString());
    }

    [Fact]
    public async Task The_return_address_defaults_to_the_local_frontend()
    {
        var handler = new StubHttpHandler((_, _) => Json(HttpStatusCode.OK, OkResponse));
        var order = OrderOf(("Latte", "Small", 1, 130m));

        await Gateway(handler, frontend: null).CreateCheckoutAsync(order, default);

        Assert.StartsWith("http://localhost:3000/orders/", Attributes(handler.Body!).GetProperty("success_url").GetString());
    }

    [Fact]
    public async Task Payment_methods_default_to_gcash_qrph_and_card_unless_configured()
    {
        var defaults = new StubHttpHandler((_, _) => Json(HttpStatusCode.OK, OkResponse));
        await Gateway(defaults).CreateCheckoutAsync(OrderOf(("Latte", "Small", 1, 130m)), default);
        Assert.Equal(
            ["gcash", "paymaya", "grab_pay", "qrph", "card"],
            Attributes(defaults.Body!).GetProperty("payment_method_types").EnumerateArray().Select(e => e.GetString()));

        var custom = new StubHttpHandler((_, _) => Json(HttpStatusCode.OK, OkResponse));
        await Gateway(custom, methods: ["gcash", "paymaya"]).CreateCheckoutAsync(OrderOf(("Latte", "Small", 1, 130m)), default);
        Assert.Equal(
            ["gcash", "paymaya"],
            Attributes(custom.Body!).GetProperty("payment_method_types").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Without_a_secret_key_it_fails_before_calling_the_network()
    {
        var handler = new StubHttpHandler((_, _) => Json(HttpStatusCode.OK, OkResponse));

        await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            Gateway(handler, secretKey: "").CreateCheckoutAsync(OrderOf(("Latte", "Small", 1, 130m)), default));

        Assert.Null(handler.Request);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task A_rejection_from_paymongo_becomes_a_gateway_exception(HttpStatusCode status)
    {
        var handler = new StubHttpHandler((_, _) => Json(status, """{"errors":[{"detail":"nope"}]}"""));

        await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            Gateway(handler).CreateCheckoutAsync(OrderOf(("Latte", "Small", 1, 130m)), default));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{"data":{"id":"cs_abc"}}""")]       // the checkout URL is missing
    public async Task A_response_we_cannot_read_becomes_a_gateway_exception(string body)
    {
        var handler = new StubHttpHandler((_, _) => Json(HttpStatusCode.OK, body));

        await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            Gateway(handler).CreateCheckoutAsync(OrderOf(("Latte", "Small", 1, 130m)), default));
    }

    [Fact]
    public async Task A_network_failure_becomes_a_gateway_exception()
    {
        var handler = new StubHttpHandler((_, _) => throw new HttpRequestException("connection refused"));

        await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            Gateway(handler).CreateCheckoutAsync(OrderOf(("Latte", "Small", 1, 130m)), default));
    }
}