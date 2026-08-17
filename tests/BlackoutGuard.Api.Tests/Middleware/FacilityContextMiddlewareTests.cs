using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using BlackoutGuard.Api.Middleware;
using BlackoutGuard.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace BlackoutGuard.Api.Tests.Middleware;

public class FacilityContextMiddlewareTests : IAsyncLifetime
{
    private const string SigningKey = "facility-context-test-signing-key-0123456789abcdef";
    private const string TestDatabase = "blackoutguard_v2_middleware_test";

    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly Guid _facilityId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await EnsureTestDatabaseAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddDbContext<BlackoutGuardDbContext>(options =>
            options.UseNpgsql(
                $"Host=localhost;Database={TestDatabase};Username=postgres;Password=postgres"));

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey))
                };
            });
        builder.Services.AddAuthorization();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseMiddleware<FacilityContextMiddleware>();
        _app.UseAuthorization();

        _app.MapGet("/test/current-facility", [Authorize] async (BlackoutGuardDbContext db) =>
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT current_setting('app.current_facility_id', true)";
            var result = await command.ExecuteScalarAsync();

            return Results.Ok(new { facilityId = result as string });
        });

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
        await DropTestDatabaseAsync();
    }

    [Fact]
    public async Task ValidFacilityClaim_SetsSessionVariable_ConfirmByQueryBack()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", CreateToken(facilityClaim: _facilityId));

        var response = await _client.GetAsync("/test/current-facility");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CurrentFacilityResponse>();
        Assert.NotNull(body);
        Assert.Equal(_facilityId.ToString(), body!.FacilityId);
    }

    [Fact]
    public async Task MissingFacilityClaim_Returns403Forbidden()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", CreateToken(facilityClaim: null));

        var response = await _client.GetAsync("/test/current-facility");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static string CreateToken(Guid? facilityClaim)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user"),
            new(ClaimTypes.Role, "Admin")
        };

        if (facilityClaim.HasValue)
        {
            claims.Add(new Claim("facility_id", facilityClaim.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task EnsureTestDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(
            "Host=localhost;Database=postgres;Username=postgres;Password=postgres");
        await connection.OpenAsync();

        await using (var drop = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{TestDatabase}\" WITH (FORCE)", connection))
        {
            await drop.ExecuteNonQueryAsync();
        }

        await using (var create = new NpgsqlCommand(
            $"CREATE DATABASE \"{TestDatabase}\"", connection))
        {
            await create.ExecuteNonQueryAsync();
        }
    }

    private static async Task DropTestDatabaseAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(
                "Host=localhost;Database=postgres;Username=postgres;Password=postgres");
            await connection.OpenAsync();

            await using var drop = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{TestDatabase}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    private sealed class CurrentFacilityResponse
    {
        public string? FacilityId { get; set; }
    }
}
