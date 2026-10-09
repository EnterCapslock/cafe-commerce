using Cafe.Api.Models.Enums;
namespace Cafe.Api.Models.Dtos;

public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);

public record UserDto(int Id, string Name, string Email, Role Role);

public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);