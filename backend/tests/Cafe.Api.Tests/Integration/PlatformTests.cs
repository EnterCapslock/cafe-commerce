using System.Net;
using System.Text.Json;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;
using Cafe.Api.Tests.TestSupport;
using Xunit;

namespace Cafe.Api.Tests.Integration;

public class PlatformTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private HttpClient Client => api.Client;

    [Fact]
    public async Task Health_checks_report_the_database_and_the_process()
    {
        var live = await Client.Call(HttpMethod.Get, "/health/live");
        await live.ExpectAsync(HttpStatusCode.OK);

        var health = await Client.Call(HttpMethod.Get, "/health");
        await health.ExpectAsync(HttpStatusCode.OK);
        using var report = JsonDocument.Parse(await health.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", report.RootElement.GetProperty("status").GetString());
        var checks = report.RootElement.GetProperty("checks").EnumerateArray().ToList();
        Assert.Contains(checks, c =>
            c.GetProperty("name").GetString() == "database" && c.GetProperty("status").GetString() == "Healthy");
    }

    [Fact]
    public async Task A_fresh_database_gets_the_peso_menu_and_a_working_admin()
    {
        var menu = await (await Client.Call(HttpMethod.Get, "/api/products")).ReadAsync<List<ProductSummaryDto>>();
        Assert.True(menu.Count >= 15);
        Assert.All(menu.Where(p => p.Category == ProductCategory.Coffee), p => Assert.Equal(ProductCategory.Coffee, p.Category));

        var flatWhite = Assert.Single(menu, p => p.Name == "Flat White");
        var detail = await (await Client.Call(HttpMethod.Get, $"/api/products/{flatWhite.Id}")).ReadAsync<ProductDetailDto>();
        Assert.Equal([130m, 150m, 170m], detail.Variants.Select(v => v.Price));

        Assert.False(string.IsNullOrEmpty(await api.AdminTokenAsync()));
    }

    [Fact]
    public async Task Errors_come_back_as_problem_details()
    {
        var badCategory = await Client.Call(HttpMethod.Get, "/api/products?category=Pizza");
        await badCategory.ExpectAsync(HttpStatusCode.BadRequest);
        AssertProblemShape(await badCategory.Content.ReadAsStringAsync(), 400);

        var missing = await Client.Call(HttpMethod.Get, "/api/products/999999");
        await missing.ExpectAsync(HttpStatusCode.NotFound);
        AssertProblemShape(await missing.Content.ReadAsStringAsync(), 404);

        var unknownRoute = await Client.Call(HttpMethod.Get, "/api/nothing-here");
        await unknownRoute.ExpectAsync(HttpStatusCode.NotFound);
        AssertProblemShape(await unknownRoute.Content.ReadAsStringAsync(), 404);
    }

    private static void AssertProblemShape(string body, int status)
    {
        using var document = JsonDocument.Parse(body);
        Assert.Equal(status, document.RootElement.GetProperty("status").GetInt32());
        Assert.True(document.RootElement.TryGetProperty("title", out _));
    }

    [Fact]
    public async Task Only_the_configured_website_may_call_the_api_from_a_browser()
    {
        var allowed = await Client.Call(HttpMethod.Get, "/api/products",
            configure: r => r.Headers.Add("Origin", TestEnvironment.AllowedOrigin));
        Assert.Equal(TestEnvironment.AllowedOrigin, allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());

        var stranger = await Client.Call(HttpMethod.Get, "/api/products",
            configure: r => r.Headers.Add("Origin", "http://evil.example"));
        Assert.False(stranger.Headers.Contains("Access-Control-Allow-Origin"));

        var preflight = await Client.Call(HttpMethod.Options, "/api/orders", configure: r =>
        {
            r.Headers.Add("Origin", TestEnvironment.AllowedOrigin);
            r.Headers.Add("Access-Control-Request-Method", "POST");
            r.Headers.Add("Access-Control-Request-Headers", "content-type");
        });
        Assert.True(preflight.IsSuccessStatusCode);
        Assert.Equal(TestEnvironment.AllowedOrigin, preflight.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }
}