using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Cafe.Api.Data;
using Cafe.Api.Extensions;
using Cafe.Api.Middleware;
using Cafe.Api.Services;
using Cafe.Api.Services.Interfaces;
using Cafe.Api.Validators;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Data ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

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

// --- Rate limiting ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

    options.AddPolicy("orders", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
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
builder.Services.AddHealthChecks();

// --- Options & clock ---
builder.Services.Configure<OrderOptions>(
    builder.Configuration.GetSection(OrderOptions.SectionName));
builder.Services.Configure<PayMongoOptions>(
    builder.Configuration.GetSection(PayMongoOptions.SectionName));
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
builder.Services.AddHostedService<OrderCleanupService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

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
app.MapHealthChecks("/health");

app.Run();

public partial class Program;