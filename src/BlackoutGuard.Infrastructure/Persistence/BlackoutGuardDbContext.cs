using Microsoft.EntityFrameworkCore;
using BlackoutGuard.Infrastructure.Persistence.Models;
using BlackoutGuard.Infrastructure.Persistence.Configurations;

namespace BlackoutGuard.Infrastructure.Persistence;

public class BlackoutGuardDbContext : DbContext
{
    public BlackoutGuardDbContext(DbContextOptions<BlackoutGuardDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Load> Loads => Set<Load>();
    public DbSet<Rule> Rules => Set<Rule>();
    public DbSet<TimeSchedule> TimeSchedules => Set<TimeSchedule>();
    public DbSet<LoadCooldownState> LoadCooldownStates => Set<LoadCooldownState>();
    public DbSet<User> Users => Set<User>();
    public DbSet<DecisionAuditLog> DecisionAuditLogs => Set<DecisionAuditLog>();
    public DbSet<AlarmRecord> AlarmRecords => Set<AlarmRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new FacilityConfiguration());
        modelBuilder.ApplyConfiguration(new ZoneConfiguration());
        modelBuilder.ApplyConfiguration(new LoadConfiguration());
        modelBuilder.ApplyConfiguration(new RuleConfiguration());
        modelBuilder.ApplyConfiguration(new TimeScheduleConfiguration());
        modelBuilder.ApplyConfiguration(new LoadCooldownStateConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new DecisionAuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new AlarmRecordConfiguration());
    }
}
