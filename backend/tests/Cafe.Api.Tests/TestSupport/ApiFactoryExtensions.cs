using System.Net;
using System.Text;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;

namespace Cafe.Api.Tests.TestSupport;

/// <summary>Everyday actions the tests use over and over, so each test reads like a story.</summary>
public static class ApiFactoryExtensions
{
    public static async Task<HttpResponseMessage> AdminAsync(
        this ApiFactory api, HttpMethod method, string url, object? body = null) =>
        await api.Client.Call(method, url, body, await api.AdminTokenAsync());

    public static async Task<(string Token, UserDto User)> RegisterCustomerAsync(this ApiFactory api, string? name = null)
    {
        var email = $"customer-{Guid.NewGuid():N}@example.com";
        var response = await api.Client.Call(HttpMethod.Post, "/api/auth/register",
            new { name = name ?? "Test Customer", email, password = TestData.StrongPassword });
        var auth = await response.ReadAsync<AuthResponse>(HttpStatusCode.Created);
        return (auth.Token, auth.User);
    }

    /// <summary>A new menu item in the Merchandise category (the seeded Coffee and Food stay untouched).</summary>
    public static async Task<AdminProductDto> CreateProductAsync(
        this ApiFactory api, (string Name, decimal Price)[] variants, string? name = null, bool isActive = true)
    {
        var input = new ProductInput(
            name ?? TestData.Unique("Item"),
            "Created by an integration test.",
            ProductCategory.Merchandise,
            isActive,
            variants.Select(v => new VariantInput(null, v.Name, v.Price, true)).ToList());

        var response = await api.AdminAsync(HttpMethod.Post, "/api/admin/products", input);
        return await response.ReadAsync<AdminProductDto>(HttpStatusCode.Created);
    }

    /// <summary>An order request that is valid right now on the API's clock.</summary>
    public static CreateOrderRequest BuildOrder(
        this ApiFactory api,
        int variantId,
        int quantity = 1,
        PaymentMethod method = PaymentMethod.PayAtCounter,
        string? name = null,
        string? email = null,
        string? phone = null,
        int pickupMinutes = 60) =>
        new(name ?? TestData.Unique("Customer"),
            email ?? "customer@example.com",
            phone ?? "09171234567",
            api.Time.GetUtcNow().AddMinutes(pickupMinutes),
            [new OrderLineRequest(variantId, quantity)],
            method);

    public static async Task<OrderDto> PlaceOrderAsync(this ApiFactory api, CreateOrderRequest request, string? token = null)
    {
        var response = await api.Client.Call(HttpMethod.Post, "/api/orders", request, token);
        return await response.ReadAsync<OrderDto>(HttpStatusCode.Created);
    }

    public static async Task<OrderDto> GetOrderAsync(this ApiFactory api, Guid id, string? token = null)
    {
        var response = await api.Client.Call(HttpMethod.Get, $"/api/orders/{id}", token: token);
        return await response.ReadAsync<OrderDto>();
    }

    /// <summary>The admin order list. Pass a query like "?status=Completed" or "?search=ana".</summary>
    public static async Task<List<OrderDto>> GetBoardAsync(this ApiFactory api, string query = "")
    {
        var response = await api.AdminAsync(HttpMethod.Get, $"/api/admin/orders{query}");
        return await response.ReadAsync<List<OrderDto>>();
    }

    public static Task<HttpResponseMessage> TryChangeStatusAsync(this ApiFactory api, Guid id, OrderStatus status) =>
        api.AdminAsync(HttpMethod.Patch, $"/api/admin/orders/{id}/status", new UpdateOrderStatusRequest(status));

    public static async Task<OrderDto> ChangeStatusAsync(this ApiFactory api, Guid id, OrderStatus status) =>
        await (await api.TryChangeStatusAsync(id, status)).ReadAsync<OrderDto>();

    public static async Task<HttpResponseMessage> PostWebhookAsync(this ApiFactory api, string body, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (signature is not null) request.Headers.TryAddWithoutValidation("Paymongo-Signature", signature);
        return await api.Client.SendAsync(request);
    }

    /// <summary>Delivers a correctly signed "payment received" event for an online order.</summary>
    public static async Task MarkPaidAsync(this ApiFactory api, OrderDto order)
    {
        var body = Webhooks.PaidEvent(api.PayMongo.SessionFor(order.Id), order.Id.ToString());
        var response = await api.PostWebhookAsync(body, Webhooks.Sign(body));
        await response.ExpectAsync(HttpStatusCode.OK);
    }
}