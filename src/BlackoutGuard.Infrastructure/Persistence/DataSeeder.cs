using BlackoutGuard.Infrastructure.Persistence;
using BlackoutGuard.Infrastructure.Persistence.Models;
using BlackoutGuard.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace BlackoutGuard.Infrastructure.Persistence;

public static class DataSeeder
{
    public const string AdminEmail = "admin@test.com";
    public const string AdminPassword = "Admin123!";

    public static async Task SeedAsync(BlackoutGuardDbContext context)
    {
        if (await context.Users.AnyAsync(u => u.Email == AdminEmail))
            return;

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "E2E Test Tenant",
            Plan = "trial"
        };

        var facility = new Facility
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "E2E Test Facility",
            GeneratorCapacityKW = 500
        };

        var admin = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = AdminEmail,
            PasswordHash = PasswordHasher.Hash(AdminPassword),
            Role = "Admin",
            IsActive = true
        };

        context.Tenants.Add(tenant);
        context.Facilities.Add(facility);
        context.Users.Add(admin);

        await context.SaveChangesAsync();
    }
}
