using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackoutGuard.Infrastructure.Persistence.Models;

namespace BlackoutGuard.Infrastructure.Persistence.Configurations;

public class FacilityConfiguration : IEntityTypeConfiguration<Facility>
{
    public void Configure(EntityTypeBuilder<Facility> builder)
    {
        builder.ToTable("facilities");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .ValueGeneratedOnAdd();

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(f => f.GeneratorCapacityKW)
            .IsRequired();

        builder.Property(f => f.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasOne(f => f.Tenant)
            .WithMany(t => t.Facilities)
            .HasForeignKey(f => f.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.TenantId)
            .HasDatabaseName("idx_facilities_tenant");

        builder.HasMany(f => f.Zones)
            .WithOne(z => z.Facility)
            .HasForeignKey(z => z.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.Loads)
            .WithOne(l => l.Facility)
            .HasForeignKey(l => l.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.Rules)
            .WithOne(r => r.Facility)
            .HasForeignKey(r => r.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.TimeSchedules)
            .WithOne(ts => ts.Facility)
            .HasForeignKey(ts => ts.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.DecisionAuditLogs)
            .WithOne(d => d.Facility)
            .HasForeignKey(d => d.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.AlarmRecords)
            .WithOne(a => a.Facility)
            .HasForeignKey(a => a.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
