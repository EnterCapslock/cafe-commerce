using Cafe.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cafe.Api.Tests.Unit.Auth;

public class JwtConfigurationTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();

    private static readonly (string, string) Issuer = ("Jwt:Issuer", "cafe-api");
    private static readonly (string, string) Audience = ("Jwt:Audience", "cafe-web");

    [Fact]
    public void The_app_refuses_to_start_without_a_jwt_section()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddJwtAuth(Config()));
    }

    [Theory]
    [InlineData(31, true)]
    [InlineData(32, false)]
    public void The_signing_key_must_be_at_least_32_bytes(int length, bool shouldThrow)
    {
        var config = Config(("Jwt:Key", new string('k', length)), Issuer, Audience);
        var services = new ServiceCollection();

        if (shouldThrow) Assert.Throws<InvalidOperationException>(() => services.AddJwtAuth(config));
        else services.AddJwtAuth(config);
    }

    [Theory]
    [InlineData("", "cafe-web")]
    [InlineData("cafe-api", "")]
    public void The_issuer_and_audience_are_required(string issuer, string audience)
    {
        var config = Config(("Jwt:Key", new string('k', 40)), ("Jwt:Issuer", issuer), ("Jwt:Audience", audience));

        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddJwtAuth(config));
    }

    [Fact]
    public async Task The_admin_policy_requires_the_admin_role()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddJwtAuth(Config(("Jwt:Key", new string('k', 40)), Issuer, Audience));
        await using var provider = services.BuildServiceProvider();

        var policy = await provider.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync(Policies.AdminOnly);

        var roles = Assert.Single(policy!.Requirements.OfType<RolesAuthorizationRequirement>());
        Assert.Equal(["Admin"], roles.AllowedRoles);
    }
}