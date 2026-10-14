using System.Net;
using Cafe.Api.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cafe.Api.Tests.Unit.Platform;

public class ClientIpTests
{
    private const string Secret = "unit-test-proxy-secret";
    private const string DirectCaller = "203.0.113.9";

    private static HttpContext Context(string configuredSecret, string? secretHeader, string? ipHeader)
    {
        var services = new ServiceCollection()
            .AddSingleton(Options.Create(new ProxyOptions { SharedSecret = configuredSecret }))
            .BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = services };
        context.Connection.RemoteIpAddress = IPAddress.Parse(DirectCaller);
        if (secretHeader is not null) context.Request.Headers[ClientIp.SecretHeader] = secretHeader;
        if (ipHeader is not null) context.Request.Headers[ClientIp.IpHeader] = ipHeader;
        return context;
    }

    [Theory]
    [InlineData("198.51.100.7")]
    [InlineData("2001:db8::1")]
    public void The_visitors_ip_is_used_when_the_proxy_proves_who_it_is(string visitor)
    {
        Assert.Equal(visitor, ClientIp.Resolve(Context(Secret, Secret, visitor)));
    }

    [Fact]
    public void A_wrong_secret_is_ignored_so_the_header_cannot_be_spoofed()
    {
        Assert.Equal(DirectCaller, ClientIp.Resolve(Context(Secret, "wrong-secret", "198.51.100.7")));
    }

    [Fact]
    public void The_ip_header_is_ignored_when_no_secret_is_configured()
    {
        Assert.Equal(DirectCaller, ClientIp.Resolve(Context(configuredSecret: "", secretHeader: "", ipHeader: "198.51.100.7")));
    }

    [Fact]
    public void A_missing_secret_header_means_the_direct_address_is_used()
    {
        Assert.Equal(DirectCaller, ClientIp.Resolve(Context(Secret, null, "198.51.100.7")));
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("")]
    [InlineData("198.51.100.7, 10.0.0.1")]
    public void An_invalid_ip_header_falls_back_to_the_direct_address(string claimed)
    {
        Assert.Equal(DirectCaller, ClientIp.Resolve(Context(Secret, Secret, claimed)));
    }

    [Fact]
    public void Without_any_headers_the_connection_address_is_used()
    {
        Assert.Equal(DirectCaller, ClientIp.Resolve(Context(Secret, null, null)));
    }
}