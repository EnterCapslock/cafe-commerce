using Cafe.Api.Models.Dtos;
namespace Cafe.Api.Services.Interfaces;

public interface IAuthService
{
    /// <returns>null if the email is already registered.</returns>
    Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken ct);

    /// <returns>null if the credentials are invalid.</returns>
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct);

    Task<UserDto?> GetUserAsync(int id, CancellationToken ct);
}