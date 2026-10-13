using System.Net;
using System.Security.Claims;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;
using Cafe.Api.Tests.TestSupport;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Xunit;

namespace Cafe.Api.Tests.Integration;

public class AuthTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private HttpClient Client => api.Client;

    private static string NewEmail() => $"auth-{Guid.NewGuid():N}@example.com";

    private static string ForgeToken(string key, DateTime notBefore, DateTime expires, string role)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim("sub", "1"), new Claim("name", "Forged"), new Claim("email", "forged@example.com"), new Claim("role", role)
            ]),
            Issuer = TestEnvironment.JwtIssuer,
            Audience = TestEnvironment.JwtAudience,
            NotBefore = notBefore,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    [Fact]
    public async Task Registering_creates_a_customer_and_lowercases_the_email()
    {
        var email = $"Mixed.Case-{Guid.NewGuid():N}@Example.com";

        var response = await Client.Call(HttpMethod.Post, "/api/auth/register",
            new { name = "  Juan Dela Cruz  ", email, password = TestData.StrongPassword });
        var auth = await response.ReadAsync<AuthResponse>(HttpStatusCode.Created);

        Assert.Equal(Role.Customer, auth.User.Role);
        Assert.Equal(email.ToLowerInvariant(), auth.User.Email);
        Assert.Equal("Juan Dela Cruz", auth.User.Name);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
    }

    [Fact]
    public async Task Nobody_can_register_as_an_admin()
    {
        var response = await Client.Call(HttpMethod.Post, "/api/auth/register",
            new { name = "Sneaky", email = NewEmail(), password = TestData.StrongPassword, role = "Admin" });
        var auth = await response.ReadAsync<AuthResponse>(HttpStatusCode.Created);

        Assert.Equal(Role.Customer, auth.User.Role);

        var adminAttempt = await Client.Call(HttpMethod.Get, "/api/admin/products", token: auth.Token);
        Assert.Equal(HttpStatusCode.Forbidden, adminAttempt.StatusCode);
    }

    [Fact]
    public async Task The_same_email_cannot_register_twice_whatever_its_case()
    {
        var email = NewEmail();
        await Client.Call(HttpMethod.Post, "/api/auth/register",
            new { name = "First", email, password = TestData.StrongPassword });

        var again = await Client.Call(HttpMethod.Post, "/api/auth/register",
            new { name = "Second", email = email.ToUpperInvariant(), password = TestData.StrongPassword });

        var problem = await again.ReadProblemAsync(HttpStatusCode.Conflict);
        Assert.Equal("That email is already registered.", problem.Title);
    }

    [Fact]
    public async Task Weak_passwords_and_bad_emails_are_rejected_with_field_errors()
    {
        var response = await Client.Call(HttpMethod.Post, "/api/auth/register",
            new { name = "Someone", email = "not-an-email", password = "short" });

        var problem = await response.ReadValidationAsync();
        Assert.NotEmpty(problem.Errors["password"]);
        Assert.NotEmpty(problem.Errors["email"]);
    }

    [Fact]
    public async Task Login_works_regardless_of_email_case()
    {
        var email = NewEmail();
        await Client.Call(HttpMethod.Post, "/api/auth/register",
            new { name = "Casey", email, password = TestData.StrongPassword });

        var response = await Client.Call(HttpMethod.Post, "/api/auth/login",
            new { email = email.ToUpperInvariant(), password = TestData.StrongPassword });

        var auth = await response.ReadAsync<AuthResponse>();
        Assert.Equal(email, auth.User.Email);
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_email_look_exactly_the_same()
    {
        var email = NewEmail();
        await Client.Call(HttpMethod.Post, "/api/auth/register",
            new { name = "Sam", email, password = TestData.StrongPassword });

        var wrongPassword = await Client.Call(HttpMethod.Post, "/api/auth/login",
            new { email, password = "Wrong#Pass1" });
        var unknownEmail = await Client.Call(HttpMethod.Post, "/api/auth/login",
            new { email = NewEmail(), password = TestData.StrongPassword });

        var first = await wrongPassword.ReadProblemAsync(HttpStatusCode.Unauthorized);
        var second = await unknownEmail.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("Invalid email or password.", first.Title);
        Assert.Equal(first.Title, second.Title);
    }

    [Fact]
    public async Task The_profile_endpoint_needs_a_valid_token()
    {
        var (token, user) = await api.RegisterCustomerAsync("Profile Person");

        var me = await (await Client.Call(HttpMethod.Get, "/api/auth/me", token: token)).ReadAsync<UserDto>();
        Assert.Equal(user.Id, me.Id);
        Assert.Equal("Profile Person", me.Name);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.Call(HttpMethod.Get, "/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.Call(HttpMethod.Get, "/api/auth/me", token: "garbage")).StatusCode);
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_rejected_even_if_it_claims_to_be_an_admin()
    {
        var now = DateTime.UtcNow;
        var forged = ForgeToken("a-completely-different-key-of-32-bytes!!", now.AddMinutes(-1), now.AddMinutes(30), "Admin");

        var response = await Client.Call(HttpMethod.Get, "/api/admin/products", token: forged);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_expired_token_is_rejected()
    {
        var now = DateTime.UtcNow;
        var expired = ForgeToken(TestEnvironment.JwtKey, now.AddHours(-2), now.AddMinutes(-5), "Customer");

        var response = await Client.Call(HttpMethod.Get, "/api/auth/me", token: expired);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/admin/products")]
    [InlineData("GET", "/api/admin/orders")]
    [InlineData("POST", "/api/admin/products")]
    [InlineData("PUT", "/api/admin/products/1")]
    [InlineData("DELETE", "/api/admin/products/1")]
    [InlineData("POST", "/api/admin/products/1/image")]
    [InlineData("PATCH", "/api/admin/orders/00000000-0000-0000-0000-000000000001/status")]
    public async Task Admin_endpoints_turn_away_visitors_and_customers(string method, string url)
    {
        var (customerToken, _) = await api.RegisterCustomerAsync();
        var http = new HttpMethod(method);

        // The upload endpoint only accepts a multipart form, and the framework checks that before the
        // login check (a JSON request would get 415), so send what it expects.
        var isUpload = url.EndsWith("/image", StringComparison.Ordinal);
        Task<HttpResponseMessage> Send(string? token) => isUpload
            ? Client.Call(http, url, token: token, configure: r => r.Content = UploadForm())
            : Client.Call(http, url, new { }, token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(customerToken)).StatusCode);
    }

    private static MultipartFormDataContent UploadForm()
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([0xFF, 0xD8, 0xFF]), "file", "photo.jpg");
        return form;
    }

    [Theory]
    [InlineData("/api/admin/products")]
    [InlineData("/api/admin/orders")]
    public async Task Admin_endpoints_open_for_the_seeded_admin(string url)
    {
        var response = await api.AdminAsync(HttpMethod.Get, url);

        await response.ExpectAsync(HttpStatusCode.OK);
    }
}