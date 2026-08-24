using System;
using System.Threading.Tasks;
using BlackoutGuard.Infrastructure.Persistence;
using BlackoutGuard.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BlackoutGuard.Infrastructure.Persistence;

public static class DataSeeder
{
    public static async Task SeedAsync(BlackoutGuardDbContext context)
    {
        // Tenant နှင့် Facility မရှိသေးလျှင် Default အနေဖြင့် Auto Create လုပ်ပေးမည်
        if (!await context.Tenants.AnyAsync())
        {
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
                GeneratorCapacityKw = 500
            };

            context.Tenants.Add(tenant);
            context.Facilities.Add(facility);

            await context.SaveChangesAsync();
        }
    }
}