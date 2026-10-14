using System.Text;
using Cafe.Api.Models.Entities;
using Cafe.Api.Models.Enums;
using Cafe.Api.Services.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Cafe.Api.Tests.Unit.Auth;

public class TokenServiceTests
{
    private const string Key = "unit-test-signing-key-at-least-32-bytes!!";

    private static readonly JwtOptions Jwt = new()
    {
        Key = Key, Issuer = "cafe-api", Audience = "cafe-web", ExpiryMinutes = 60
    };

    private static readonly User Admin = new() { Id = 7, Name = "Cafe Admin", Email = "admin@cafe.local", Role = Role.Admin };

    private static TokenService Service(JwtOptions? options = null) => new(Options.Create(options ?? Jwt));

    private static TokenValidationParameters Parameters(string key = Key, string audience = "cafe-web") => new()
    {
        ValidateIssuer = true, ValidIssuer = "cafe-api",
        ValidateAudience = true, ValidAudience = audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateLifetime = true,
        NameClaimType = "name", RoleClaimType = "role"
    };

    [Fact]
    public void The_token_carries_the_short_claim_names_the_api_reads()
    {
        var (token, _) = Service().CreateToken(Admin);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        Assert.Equal("7", jwt.GetClaim("sub").Value);
        Assert.Equal("admin@cafe.local", jwt.GetClaim("email").Value);
        Assert.Equal("Cafe Admin", jwt.GetClaim("name").Value);
        Assert.Equal("Admin", jwt.GetClaim("role").Value);
        Assert.Equal("cafe-api", jwt.Issuer);
        Assert.Contains("cafe-web", jwt.Audiences);
    }

    [Fact]
    public void The_token_never_contains_the_password_hash()
    {
        var user = new User { Id = 1, Name = "A", Email = "a@b.com", PasswordHash = "$2a$11$secrethashvalue", Role = Role.Customer };
        var (token, _) = Service().CreateToken(user);

        Assert.DoesNotContain("secrethashvalue", token);
        Assert.DoesNotContain("PasswordHash", new JsonWebTokenHandler().ReadJsonWebToken(token).EncodedPayload);
    }

    [Fact]
    public void The_expiry_is_the_configured_number_of_minutes_from_now()
    {
        var (_, expiresAt) = Service().CreateToken(Admin);

        Assert.InRange((expiresAt - DateTime.UtcNow).TotalMinutes, 59.9, 60.1);
    }

    [Fact]
    public async Task A_token_validates_with_the_configured_key_issuer_and_audience()
    {
        var (token, _) = Service().CreateToken(Admin);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, Parameters());

        Assert.True(result.IsValid);
        Assert.Equal("Admin", result.ClaimsIdentity.FindFirst("role")?.Value);
    }

    [Fact]
    public async Task A_token_is_rejected_with_a_different_signing_key()
    {
        var (token, _) = Service().CreateToken(Admin);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, Parameters(key: "a-completely-different-key-of-32-bytes!!"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task A_token_is_rejected_for_a_different_audience()
    {
        var (token, _) = Service().CreateToken(Admin);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, Parameters(audience: "some-other-app"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task A_tampered_token_is_rejected()
    {
        var (token, _) = Service().CreateToken(new User { Id = 2, Name = "Shopper", Email = "s@x.com", Role = Role.Customer });
        var parts = token.Split('.');
        var forgedPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            """{"sub":"2","role":"Admin","iss":"cafe-api","aud":"cafe-web"}""")).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var result = await new JsonWebTokenHandler().ValidateTokenAsync($"{parts[0]}.{forgedPayload}.{parts[2]}", Parameters());

        Assert.False(result.IsValid);
    }
}