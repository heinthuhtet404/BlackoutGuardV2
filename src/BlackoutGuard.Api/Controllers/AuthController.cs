using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Api.Services;
using BlackoutGuard.Infrastructure.Persistence;
using BlackoutGuard.Infrastructure.Persistence.Models;
using BlackoutGuard.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// User class နာမည် ထပ်နေမှု CS0104 error ကို ဖြေရှင်းရန် Namespace Alias သတ်မှတ်ခြင်း
using DomainUser = BlackoutGuard.Domain.Entities.User;

namespace BlackoutGuard.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepo;
    private readonly JwtTokenService _jwtTokenService;
    private readonly BlackoutGuardDbContext _dbContext;

    public AuthController(
        IUserRepository userRepo,
        JwtTokenService jwtTokenService,
        BlackoutGuardDbContext dbContext)
    {
        _userRepo = userRepo;
        _jwtTokenService = jwtTokenService;
        _dbContext = dbContext;
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

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required." });

        var existingUser = await _userRepo.GetByEmailAsync(request.Email, ct);
        if (existingUser is not null)
            return BadRequest(new { error = "User with this email already exists." });

        // 1. AUTO-ADMIN Logic
        var hasAnyUser = await _userRepo.HasAnyUserAsync(ct);
        var role = hasAnyUser ? "Operator" : "Admin";

        // 2. Tenant Record အသစ်ကို Create လုပ်ပါ
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = string.IsNullOrWhiteSpace(request.OrganizationName) ? $"{request.FullName}'s Org" : request.OrganizationName,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.Tenants.AddAsync(tenant, ct);

        // 3. User Record ဖန်တီးပါ (DomainUser တိုက်ရိုက် သုံးစွဲထားသည်)
        var userId = Guid.NewGuid();
        var passwordHash = PasswordHasher.Hash(request.Password);

        var newUser = new DomainUser
        {
            Id = userId,
            TenantId = tenantId,
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = passwordHash,
            Role = role,
            OrganizationName = request.OrganizationName,
            GeneratorCapacity = request.GeneratorCapacity,
            FacilityLocation = request.FacilityLocation,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepo.AddAsync(newUser, ct);
        await _userRepo.SaveChangesAsync(ct);

        // 4. Token ထုတ်ပေးပြီး Return ပြန်ခြင်း
        var createdUser = await _userRepo.GetByEmailAsync(newUser.Email, ct);
        if (createdUser is null)
            return StatusCode(500, new { error = "Failed to retrieve registered user." });

        var (accessToken, refreshToken) = _jwtTokenService.CreateTokens(createdUser);

        return Ok(new
        {
            accessToken,
            refreshToken,
            user = new
            {
                id = createdUser.Id.ToString(),
                email = createdUser.Email,
                role = createdUser.Role,
                facilityId = createdUser.FacilityId.ToString()
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

public class RegisterRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public double GeneratorCapacity { get; set; }
    public string? FacilityLocation { get; set; }
}

public class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}