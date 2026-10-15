using System.Net;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Services.Orders;
using Cafe.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Cafe.Api.Tests.Integration;

public class OrderLookupTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private HttpClient Client => api.Client;

    private Task<HttpResponseMessage> Lookup(string code, string email) =>
        Client.Call(HttpMethod.Post, "/api/orders/lookup", new { code, email });

    private async Task<(OrderDto Order, string Email)> GuestOrderAsync()
    {
        var product = await api.CreateProductAsync([("Regular", 100m)]);
        var email = $"lookup-{Guid.NewGuid():N}@example.com";
        var order = await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id, email: email));
        return (order, email);
    }

    [Fact]
    public async Task A_guest_finds_their_order_by_code_and_email_however_they_type_them()
    {
        var (order, email) = await GuestOrderAsync();
        var code = OrderCode.For(order.Id);

        foreach (var typed in new[] { code, code.ToLowerInvariant(), $"#{code}", $"{code[..4]} {code[4..]}", $"{code[..4]}-{code[4..]}" })
        {
            var response = await Lookup(typed, email.ToUpperInvariant());
            var found = await response.ReadAsync<OrderLookupResponse>();
            Assert.Equal(order.Id, found.OrderId);
        }

        // The id opens the order page like any guest link
        Assert.Equal(order.Id, (await api.GetOrderAsync(order.Id)).Id);
    }

    [Fact]
    public async Task Every_kind_of_miss_gets_exactly_the_same_answer()
    {
        var (order, email) = await GuestOrderAsync();
        var owner = await api.RegisterCustomerAsync();
        var product = await api.CreateProductAsync([("Regular", 100m)]);
        var accountEmail = $"account-{Guid.NewGuid():N}@example.com";
        var accountOrder = await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id, email: accountEmail), owner.Token);

        var misses = new[]
        {
            await Lookup(OrderCode.For(order.Id), $"someone-else-{Guid.NewGuid():N}@example.com"),   // wrong email
            await Lookup("00000000", email),                                                          // no such order
            await Lookup(OrderCode.For(accountOrder.Id), accountEmail)                               // account orders stay private
        };

        var problems = new List<ProblemDetails>();
        foreach (var miss in misses) problems.Add(await miss.ReadProblemAsync(HttpStatusCode.NotFound));

        Assert.Contains("couldn't find an order", problems[0].Title);
        Assert.All(problems, p =>
        {
            Assert.Equal(problems[0].Title, p.Title);
            Assert.Equal(problems[0].Detail, p.Detail);
        });
    }

    [Fact]
    public async Task A_malformed_code_or_email_is_rejected_with_field_errors()
    {
        var badCode = await (await Lookup("12345", "someone@example.com")).ReadValidationAsync();
        Assert.Contains("code", badCode.Errors.Keys);

        var badEmail = await (await Lookup("E18B681F", "not-an-email")).ReadValidationAsync();
        Assert.Contains("email", badEmail.Errors.Keys);
    }

    [Fact]
    public async Task Too_many_attempts_for_one_email_are_stopped_without_affecting_other_emails()
    {
        var email = $"flood-{Guid.NewGuid():N}@example.com";

        for (var i = 0; i < LookupThrottle.PermitsPerHour; i++)
            await (await Lookup("00000000", email)).ExpectAsync(HttpStatusCode.NotFound);

        var blocked = await Lookup("00000000", email);
        var problem = await blocked.ReadProblemAsync(HttpStatusCode.TooManyRequests);
        Assert.Contains("Too many attempts", problem.Title);

        await (await Lookup("00000000", $"calm-{Guid.NewGuid():N}@example.com")).ExpectAsync(HttpStatusCode.NotFound);
    }
}