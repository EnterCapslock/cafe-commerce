using System.Security.Claims;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Cafe.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Creates a customer account and returns a JWT.</summary>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ct);
        return result is null
            ? Problem(statusCode: StatusCodes.Status409Conflict, title: "That email is already registered.")
            : Created("/api/auth/me", result);
    }

    /// <summary>Exchanges email and password for a JWT.</summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        return result is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.")
            : Ok(result);
    }

    /// <summary>Returns the authenticated user's profile.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue("sub"), out var id))
            return Unauthorized();

        var user = await auth.GetUserAsync(id, ct);
        return user is null ? Unauthorized() : Ok(user);
    }
}