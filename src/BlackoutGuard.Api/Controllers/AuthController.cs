using System.Security.Claims;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Api.Services;
using BlackoutGuard.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlackoutGuard.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepo;
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(IUserRepository userRepo, JwtTokenService jwtTokenService)
    {
        _userRepo = userRepo;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required." });

        var user = await _userRepo.GetByEmailAsync(request.Email, ct);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid email or password." });

        var (accessToken, refreshToken) = _jwtTokenService.CreateTokens(user);

        return Ok(new
        {
            accessToken,
            refreshToken,
            user = new
            {
                id = user.Id.ToString(),
                email = user.Email,
                role = user.Role,
                facilityId = user.FacilityId.ToString()
            }
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { error = "Refresh token is required." });

        var principal = _jwtTokenService.ValidateRefreshToken(request.RefreshToken);
        if (principal is null)
            return Unauthorized(new { error = "Invalid or expired refresh token." });

        var email = principal.FindFirstValue(ClaimTypes.Email);
        if (email is null)
            return Unauthorized(new { error = "Invalid refresh token claims." });

        var user = await _userRepo.GetByEmailAsync(email, ct);
        if (user is null)
            return Unauthorized(new { error = "User no longer exists." });

        var (accessToken, refreshToken) = _jwtTokenService.CreateTokens(user);

        return Ok(new
        {
            accessToken,
            refreshToken,
            user = new
            {
                id = user.Id.ToString(),
                email = user.Email,
                role = user.Role,
                facilityId = user.FacilityId.ToString()
            }
        });
    }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
