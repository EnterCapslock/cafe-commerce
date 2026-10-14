using Cafe.Api.Data;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Services.Interfaces;
using Cafe.Api.Infrastructure.Payments;
using Cafe.Api.Services.Orders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Cafe.Api.Tests.TestSupport;

/// <summary>The real API, started in-process, with its own database and fakes at the edges.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    static ApiFactory() => TestEnvironment.Apply();

    private readonly string _connectionString = SharedPostgres.CreateDatabase();
    private readonly Lazy<Task<string>> _adminToken;
    private HttpClient? _client;

    public ApiFactory()
    {
        _adminToken = new Lazy<Task<string>>(async () =>
        {
            var response = await Client.Call(HttpMethod.Post, "/api/auth/login",
                new { email = TestEnvironment.AdminEmail, password = TestEnvironment.AdminPassword });
            return (await response.ReadAsync<AuthResponse>()).Token;
        });
    }

    /// <summary>The API's clock. Tests move it forward to test time-based rules.</summary>
    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    public FakePayMongoHandler PayMongo { get; } = new();
    public FakeImageStorage Images { get; } = new();

    public HttpClient Client => _client ??= CreateClient();

    public Task<string> AdminTokenAsync() => _adminToken.Value;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString
            }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);

            services.RemoveAll<IImageStorage>();
            services.AddSingleton<IImageStorage>(Images);

            // The real PayMongoGateway, talking to a fake network
            services.AddHttpClient<IPaymentGateway, PayMongoGateway>()
                .ConfigurePrimaryHttpMessageHandler(() => PayMongo);

            // The cleanup job is run by hand (RunCleanupAsync), so it can't race the tests
            var cleanup = services
                .Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(OrderCleanupService))
                .ToList();
            foreach (var descriptor in cleanup) services.Remove(descriptor);
        });
    }

    public async Task RunDbAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<T> QueryDbAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Runs the real cleanup logic once, using the fake clock.</summary>
    public Task RunCleanupAsync()
    {
        var options = new OrderOptions();
        var service = new OrderCleanupService(
            Services.GetRequiredService<IServiceScopeFactory>(),
            Time,
            Options.Create(options),
            NullLogger<OrderCleanupService>.Instance);

        return service.RunOnceAsync(options, CancellationToken.None);
    }
}