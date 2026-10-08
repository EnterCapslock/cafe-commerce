using Cafe.Api.Models.Entities;
using Cafe.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;
namespace Cafe.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAdminAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        var email = config["Seed:AdminEmail"]?.Trim().ToLowerInvariant();
        var password = config["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:AdminEmail / Seed:AdminPassword not set; skipping admin seed.");
            return;
        }

        if (await db.Users.AnyAsync(u => u.Email == email)) return;

        db.Users.Add(new User
        {
            Name = "Cafe Admin",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = Role.Admin
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded admin user {Email}", email);
    }

    public static async Task SeedMenuAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Products.AnyAsync()) return;   // dashboard owns the data from here on

        db.Products.AddRange(SeedData.CreateMenu());
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded demo menu");
    }
}