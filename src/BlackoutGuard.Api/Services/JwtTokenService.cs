using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BlackoutGuard.Application.Services;
using Microsoft.IdentityModel.Tokens;

namespace BlackoutGuard.Api.Services;

public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string AccessToken, string RefreshToken) CreateTokens(UserAuthDto user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetKey()));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var baseClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("facility_id", user.FacilityId.ToString()),
            new("tenant_id", user.TenantId.ToString())
        };

        var now = DateTime.UtcNow;

        var accessToken = new JwtSecurityToken(
            issuer: GetIssuer(),
            audience: GetAudience(),
            claims: baseClaims,
            notBefore: now,
            expires: now.AddMinutes(30),
            signingCredentials: credentials);

        var refreshClaims = new List<Claim>(baseClaims)
        {
            new("token_type", "refresh")
        };

        var refreshToken = new JwtSecurityToken(
            issuer: GetIssuer(),
            audience: GetAudience(),
            claims: refreshClaims,
            notBefore: now,
            expires: now.AddDays(7),
            signingCredentials: credentials);

        var handler = new JwtSecurityTokenHandler();
        return (handler.WriteToken(accessToken), handler.WriteToken(refreshToken));
    }

    public ClaimsPrincipal? ValidateRefreshToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetKey()));

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = GetIssuer(),
            ValidateAudience = true,
            ValidAudience = GetAudience(),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, parameters, out _);

            if (principal.FindFirst("token_type")?.Value != "refresh")
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }

    private string GetKey() =>
        _configuration["Jwt:Key"] ?? "dev-only-signing-key-change-in-production-0123456789";

    private string GetIssuer() =>
        _configuration["Jwt:Issuer"] ?? "BlackoutGuard";

    private string GetAudience() =>
        _configuration["Jwt:Audience"] ?? "BlackoutGuard.Client";
}
