using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BlackoutGuard.Infrastructure.Persistence;

public class BlackoutGuardDbContextFactory : IDesignTimeDbContextFactory<BlackoutGuardDbContext>
{
    public BlackoutGuardDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BlackoutGuardDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=blackoutguard_v2;Username=postgres;Password=postgres");
        return new BlackoutGuardDbContext(optionsBuilder.Options);
    }
}
