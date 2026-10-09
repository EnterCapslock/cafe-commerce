using Cafe.Api.Data;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Entities;
using Cafe.Api.Models.Enums;
using Cafe.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Cafe.Api.Services;

public class AuthService(AppDbContext db, ITokenService tokens, ILogger<AuthService> logger) : IAuthService
{
    // Verified against when the email doesn't exist, so response time doesn't reveal it.
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("timing-equalizer");

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = Normalize(request.Email);

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return null;

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = Role.Customer   // never taken from the request
        };

        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return null;   // lost a race with a concurrent registration
        }

        logger.LogInformation("Registered user {UserId}", user.Id);
        return BuildResponse(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = Normalize(request.Email);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

        var passwordOk = BCrypt.Net.BCrypt.Verify(request.Password, user?.PasswordHash ?? DummyHash);
        if (user is null || !passwordOk)
        {
            logger.LogWarning("Failed login attempt");
            return null;
        }

        return BuildResponse(user);
    }

    public async Task<UserDto?> GetUserAsync(int id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? null : ToDto(user);
    }

    private AuthResponse BuildResponse(User user)
    {
        var (token, expiresAt) = tokens.CreateToken(user);
        return new AuthResponse(token, expiresAt, ToDto(user));
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Name, u.Email, u.Role);

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}