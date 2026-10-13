namespace Cafe.Api.Tests.TestSupport;

/// <summary>
/// Configuration the API reads while it starts. Environment variables win over appsettings and
/// user-secrets, so the tests behave the same on every machine.
/// </summary>
public static class TestEnvironment
{
    public const string AdminEmail = "admin@cafe.local";
    public const string AdminPassword = "Integration#Admin1";
    public const string JwtKey = "integration-test-signing-key-at-least-32-bytes!!";
    public const string JwtIssuer = "cafe-api";
    public const string JwtAudience = "cafe-web";
    public const string PayMongoSecretKey = "sk_test_integration";
    public const string PayMongoWebhookSecret = "whsk_integration_test";
    public const string AllowedOrigin = "http://localhost:3000";

    private static readonly object Gate = new();
    private static bool _applied;

    public static void Apply()
    {
        lock (Gate)
        {
            if (_applied) return;
            _applied = true;

            Set("Jwt__Key", JwtKey);
            Set("Jwt__Issuer", JwtIssuer);
            Set("Jwt__Audience", JwtAudience);
            Set("Seed__AdminEmail", AdminEmail);
            Set("Seed__AdminPassword", AdminPassword);
            Set("PayMongo__SecretKey", PayMongoSecretKey);
            Set("PayMongo__WebhookSecret", PayMongoWebhookSecret);
            Set("PayMongo__BaseUrl", "https://paymongo.test/");
            Set("Frontend__BaseUrl", "https://cafe.test");
            Set("Cors__AllowedOrigins__0", AllowedOrigin);
            Set("RateLimits__AuthPerMinute", "100000");
            Set("RateLimits__OrdersPerMinute", "100000");
            Set("Logging__LogLevel__Default", "Warning");
            Set("Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command", "None");
            Set("Logging__LogLevel__Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware", "Error");
            Set("Logging__LogLevel__Microsoft.AspNetCore.Mvc.Filters.RequestSizeLimitFilter", "Error");

            // Safety net: each test class replaces this with its own database. If that ever failed,
            // the API would fail to start instead of quietly using a real database.
            Set("ConnectionStrings__Default", "Host=127.0.0.1;Port=1;Database=must_not_be_used;Username=x;Password=x;Timeout=1");
        }
    }

    private static void Set(string name, string value) => Environment.SetEnvironmentVariable(name, value);
}