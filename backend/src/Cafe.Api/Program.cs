using System.Text.Json.Serialization;
using Cafe.Api.Data;
using Cafe.Api.Middleware;
using Cafe.Api.Services;
using Cafe.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Data ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// --- Web API ---
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// The OpenAPI generator reads the minimal-API JSON options, so register there too.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

// --- Errors: RFC 7807 everywhere ---
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// --- CORS: explicit origins only, driven by configuration ---
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

// --- Health (used by Render/Fly) ---
builder.Services.AddHealthChecks();

// --- Application services ---
builder.Services.AddScoped<IProductService, ProductService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();   // plain 404/405 responses also become ProblemDetails

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                 // /openapi/v1.json
    app.MapScalarApiReference();      // /scalar/v1

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seederLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");

    await db.Database.MigrateAsync();
    await DbSeeder.SeedAdminAsync(db, app.Configuration, seederLogger);
    await DbSeeder.SeedMenuAsync(db, seederLogger);
}

app.UseCors("Frontend");
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;   // lets WebApplicationFactory<Program> see the entry point 