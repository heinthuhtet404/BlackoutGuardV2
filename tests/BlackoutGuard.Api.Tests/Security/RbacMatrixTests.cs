using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using BlackoutGuard.Api;
using BlackoutGuard.Api.Services;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Infrastructure.Engine;
using BlackoutGuard.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace BlackoutGuard.Api.Tests.Security;

/// <summary>
/// Custom WebApplicationFactory that disables background services for integration tests
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "RbacTestDb_" + Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // 1. Remove Postgres DbContext Options
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<BlackoutGuardDbContext>));

            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // 2. Register InMemory Database
            services.AddDbContext<BlackoutGuardDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName)
                       .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });

            // 3. Remove Background Hosted Services
            var hostedServices = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
            foreach (var service in hostedServices)
            {
                services.Remove(service);
            }
        });
    }
}

public class RbacMatrixTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _operatorClient;
    private readonly HttpClient _viewerClient;
    private readonly string _facilityId;
    private readonly string _tenantId;
    private readonly List<TestUser> _testUsers;

    public RbacMatrixTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _output = output;
        _factory = factory;
        _facilityId = Guid.NewGuid().ToString();
        _tenantId = Guid.NewGuid().ToString();
        _testUsers = new List<TestUser>();

        _adminClient = _factory.CreateClient();
        _operatorClient = _factory.CreateClient();
        _viewerClient = _factory.CreateClient();

        SeedTestData().GetAwaiter().GetResult();

        foreach (var user in _testUsers)
        {
            var client = user.Role switch
            {
                "Admin" => _adminClient,
                "Operator" => _operatorClient,
                "Viewer" => _viewerClient,
                _ => null
            };

            if (client != null && !string.IsNullOrEmpty(user.Token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", user.Token);
            }
        }
    }

    private async Task SeedTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlackoutGuardDbContext>();
        var jwtService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();

        var tenant = new Infrastructure.Persistence.Models.Tenant
        {
            Id = Guid.Parse(_tenantId),
            Name = "RBAC Test Tenant",
            Plan = "trial",
            CreatedAt = DateTime.UtcNow
        };

        var facility = new Infrastructure.Persistence.Models.Facility
        {
            Id = Guid.Parse(_facilityId),
            TenantId = Guid.Parse(_tenantId),
            Name = "RBAC Test Facility",
            GeneratorCapacityKw = 100,
            TimezoneId = "UTC",
            CreatedAt = DateTime.UtcNow
        };

        db.Tenants.Add(tenant);
        db.Facilities.Add(facility);
        await db.SaveChangesAsync();

        var adminUser = new Infrastructure.Persistence.Models.User
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Parse(_tenantId),
            Email = "admin@rbac.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            Role = "Admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var operatorUser = new Infrastructure.Persistence.Models.User
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Parse(_tenantId),
            Email = "operator@rbac.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Operator123!"),
            Role = "Operator",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var viewerUser = new Infrastructure.Persistence.Models.User
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Parse(_tenantId),
            Email = "viewer@rbac.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Viewer123!"),
            Role = "Viewer",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.AddRange(adminUser, operatorUser, viewerUser);
        await db.SaveChangesAsync();

        var adminAuth = new UserAuthDto
        {
            Id = adminUser.Id,
            TenantId = adminUser.TenantId,
            Email = adminUser.Email,
            PasswordHash = adminUser.PasswordHash,
            Role = adminUser.Role,
            FacilityId = facility.Id
        };

        var operatorAuth = new UserAuthDto
        {
            Id = operatorUser.Id,
            TenantId = operatorUser.TenantId,
            Email = operatorUser.Email,
            PasswordHash = operatorUser.PasswordHash,
            Role = operatorUser.Role,
            FacilityId = facility.Id
        };

        var viewerAuth = new UserAuthDto
        {
            Id = viewerUser.Id,
            TenantId = viewerUser.TenantId,
            Email = viewerUser.Email,
            PasswordHash = viewerUser.PasswordHash,
            Role = viewerUser.Role,
            FacilityId = facility.Id
        };

        var (adminToken, _) = jwtService.CreateTokens(adminAuth);
        var (operatorToken, _) = jwtService.CreateTokens(operatorAuth);
        var (viewerToken, _) = jwtService.CreateTokens(viewerAuth);

        _testUsers.Add(new TestUser { Email = "admin@rbac.test", Password = "Admin123!", Role = "Admin", Token = adminToken });
        _testUsers.Add(new TestUser { Email = "operator@rbac.test", Password = "Operator123!", Role = "Operator", Token = operatorToken });
        _testUsers.Add(new TestUser { Email = "viewer@rbac.test", Password = "Viewer123!", Role = "Viewer", Token = viewerToken });
    }

    [Theory]
    [MemberData(nameof(RbacTestData))]
    public async Task RbacEndpointTest(string role, string endpoint, string method, int expectedStatusCode, string description)
    {
        var user = _testUsers.FirstOrDefault(u => u.Role == role);
        Assert.NotNull(user);
        Assert.NotNull(user.Token);

        var client = role switch
        {
            "Admin" => _adminClient,
            "Operator" => _operatorClient,
            "Viewer" => _viewerClient,
            _ => null
        };
        Assert.NotNull(client);

        HttpResponseMessage response;
        var fullEndpoint = endpoint.Replace("{facilityId}", _facilityId);

        // Standard JSON payload for write requests to avoid 400 Bad Request
        var samplePayload = $"{{\"name\":\"Test Item\",\"facilityId\":\"{_facilityId}\",\"email\":\"newuser@test.com\",\"role\":\"Viewer\",\"password\":\"Pass123!\"}}";

        switch (method.ToUpper())
        {
            case "GET":
                response = await client.GetAsync(fullEndpoint);
                break;
            case "POST":
                response = await client.PostAsync(fullEndpoint, new StringContent(samplePayload, Encoding.UTF8, "application/json"));
                break;
            case "PUT":
                response = await client.PutAsync(fullEndpoint, new StringContent(samplePayload, Encoding.UTF8, "application/json"));
                break;
            case "DELETE":
                response = await client.DeleteAsync(fullEndpoint);
                break;
            default:
                throw new ArgumentException($"Unsupported method: {method}");
        }

        var statusCode = (int)response.StatusCode;
        _output.WriteLine($"Role: {role}, Endpoint: {endpoint}, Method: {method}, Expected: {expectedStatusCode}, Actual: {statusCode}, Description: {description}");

        if (expectedStatusCode == 200)
        {
            Assert.True(statusCode >= 200 && statusCode < 300, $"Expected 2xx status code, got {statusCode}");
        }
        else
        {
            Assert.Equal(expectedStatusCode, statusCode);
        }
    }

    public static IEnumerable<object[]> RbacTestData()
    {
        var testCases = new List<object[]>();

        foreach (var endpoint in new[] { "/api/v1/zones", "/api/v1/loads", "/api/v1/rules" })
        {
            foreach (var role in new[] { "Admin", "Operator", "Viewer" })
            {
                testCases.Add(new object[] { role, endpoint, "GET", 200, "Read endpoint" });
            }
        }

        var writeEndpoints = new[]
        {
            ("POST", "/api/v1/zones"),
            ("PUT", "/api/v1/zones/{facilityId}"),
            ("DELETE", "/api/v1/zones/{facilityId}"),
            ("POST", "/api/v1/loads"),
            ("PUT", "/api/v1/loads/{facilityId}"),
            ("DELETE", "/api/v1/loads/{facilityId}"),
            ("PUT", "/api/v1/rules/{facilityId}")
        };

        foreach (var (method, endpoint) in writeEndpoints)
        {
            testCases.Add(new object[] { "Admin", endpoint, method, 200, "Admin write" });
            testCases.Add(new object[] { "Operator", endpoint, method, 403, "Operator write blocked" });
            testCases.Add(new object[] { "Viewer", endpoint, method, 403, "Viewer write blocked" });
        }

        testCases.Add(new object[] { "Admin", "/api/v1/schedules", "GET", 200, "Admin get schedules" });
        testCases.Add(new object[] { "Operator", "/api/v1/schedules", "GET", 200, "Operator get schedules" });
        testCases.Add(new object[] { "Viewer", "/api/v1/schedules", "GET", 403, "Viewer get schedules blocked" });

        testCases.Add(new object[] { "Admin", "/api/v1/schedules", "POST", 200, "Admin create schedule" });
        testCases.Add(new object[] { "Operator", "/api/v1/schedules", "POST", 403, "Operator create schedule blocked" });
        testCases.Add(new object[] { "Viewer", "/api/v1/schedules", "POST", 403, "Viewer create schedule blocked" });
        testCases.Add(new object[] { "Admin", "/api/v1/schedules/{facilityId}", "DELETE", 200, "Admin delete schedule" });
        testCases.Add(new object[] { "Operator", "/api/v1/schedules/{facilityId}", "DELETE", 403, "Operator delete schedule blocked" });
        testCases.Add(new object[] { "Viewer", "/api/v1/schedules/{facilityId}", "DELETE", 403, "Viewer delete schedule blocked" });

        testCases.Add(new object[] { "Admin", "/api/v1/simulator/telemetry", "GET", 200, "Admin get telemetry" });
        testCases.Add(new object[] { "Admin", "/api/v1/simulator/telemetry", "POST", 200, "Admin post telemetry" });
        testCases.Add(new object[] { "Operator", "/api/v1/simulator/telemetry", "GET", 403, "Operator get telemetry blocked" });
        testCases.Add(new object[] { "Operator", "/api/v1/simulator/telemetry", "POST", 403, "Operator post telemetry blocked" });
        testCases.Add(new object[] { "Viewer", "/api/v1/simulator/telemetry", "GET", 403, "Viewer get telemetry blocked" });
        testCases.Add(new object[] { "Viewer", "/api/v1/simulator/telemetry", "POST", 403, "Viewer post telemetry blocked" });

        testCases.Add(new object[] { "Admin", "/api/v1/audit", "GET", 200, "Admin get audit" });
        testCases.Add(new object[] { "Operator", "/api/v1/audit", "GET", 200, "Operator get audit" });
        testCases.Add(new object[] { "Viewer", "/api/v1/audit", "GET", 200, "Viewer get audit" });

        testCases.Add(new object[] { "Admin", "/api/v1/audit/export?format=csv", "GET", 200, "Admin export audit" });
        testCases.Add(new object[] { "Operator", "/api/v1/audit/export?format=csv", "GET", 200, "Operator export audit" });
        testCases.Add(new object[] { "Viewer", "/api/v1/audit/export?format=csv", "GET", 403, "Viewer export audit blocked" });

        var userEndpoints = new[]
        {
            ("GET", "/api/v1/users"),
            ("POST", "/api/v1/users"),
            ("PUT", "/api/v1/users/{facilityId}"),
            ("DELETE", "/api/v1/users/{facilityId}")
        };

        foreach (var (method, endpoint) in userEndpoints)
        {
            testCases.Add(new object[] { "Admin", endpoint, method, 200, $"Admin {method} users" });
            testCases.Add(new object[] { "Operator", endpoint, method, 403, $"Operator {method} users blocked" });
            testCases.Add(new object[] { "Viewer", endpoint, method, 403, $"Viewer {method} users blocked" });
        }

        return testCases;
    }

    public void Dispose()
    {
        _adminClient?.Dispose();
        _operatorClient?.Dispose();
        _viewerClient?.Dispose();
    }

    private class TestUser
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }
}