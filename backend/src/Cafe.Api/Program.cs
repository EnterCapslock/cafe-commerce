using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Cafe.Api.Data;
using Cafe.Api.Extensions;
using Cafe.Api.Middleware;
using Cafe.Api.Infrastructure.Images;
using Cafe.Api.Infrastructure.Payments;
using Cafe.Api.Services.Admin;
using Cafe.Api.Services.Auth;
using Cafe.Api.Services.Catalog;
using Cafe.Api.Services.Orders;
using Cafe.Api.Services.Interfaces;
using Cafe.Api.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Hosts like Render say which port to use. Locally nothing sets PORT, so the launch settings apply.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// --- Data ---
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")));

// --- Web API ---
builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecurityTransformer>());

// --- Validation ---
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// --- Authentication & authorization ---
builder.Services.AddJwtAuth(builder.Configuration);

// --- Rate limiting (per visitor: see ClientIp) ---
var authPerMinute = builder.Configuration.GetValue("RateLimits:AuthPerMinute", 10);
var ordersPerMinute = builder.Configuration.GetValue("RateLimits:OrdersPerMinute", 20);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIp.Resolve(httpContext),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1) }));

    options.AddPolicy("orders", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIp.Resolve(httpContext),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = ordersPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

// --- Errors: RFC 7807 everywhere ---
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// --- CORS ---
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

// --- Health ---
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

// --- Options & clock ---
builder.Services.Configure<OrderOptions>(builder.Configuration.GetSection(OrderOptions.SectionName));
builder.Services.Configure<PayMongoOptions>(builder.Configuration.GetSection(PayMongoOptions.SectionName));
builder.Services.Configure<CloudinaryOptions>(builder.Configuration.GetSection(CloudinaryOptions.SectionName));
builder.Services.Configure<ProxyOptions>(builder.Configuration.GetSection(ProxyOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);

// --- Payments ---
builder.Services.AddHttpClient<IPaymentGateway, PayMongoGateway>((sp, client) =>
{
    var pm = sp.GetRequiredService<IOptions<PayMongoOptions>>().Value;
    client.BaseAddress = new Uri(pm.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

// --- Application services ---
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAdminProductService, AdminProductService>();
builder.Services.AddScoped<IAdminOrderService, AdminOrderService>();
builder.Services.AddSingleton<IImageStorage, CloudinaryImageStorage>();
builder.Services.AddHostedService<OrderCleanupService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Development always prepares the database. Elsewhere it's opt-in (Database:MigrateOnStartup=true), which
// suits one small instance. A larger setup would run migrations as a separate step of the deployment.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seederLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");

    await db.Database.MigrateAsync();
    await DbSeeder.SeedAdminAsync(db, app.Configuration, seederLogger);
    await DbSeeder.SeedMenuAsync(db, seederLogger);
}

app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });   // is the process up? (the host's probe)
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync }); // can it reach the database?

app.Run();

public partial class Program;