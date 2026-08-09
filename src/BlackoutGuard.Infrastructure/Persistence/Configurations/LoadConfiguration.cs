using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackoutGuard.Infrastructure.Persistence.Models;

namespace BlackoutGuard.Infrastructure.Persistence.Configurations;

public class LoadConfiguration : IEntityTypeConfiguration<Load>
{
    public void Configure(EntityTypeBuilder<Load> builder)
    {
        builder.ToTable("loads");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd();

        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(l => l.RelayAddress)
            .IsRequired();

        builder.Property(l => l.PowerRatingKw)
            .IsRequired();

        builder.Property(l => l.Priority)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(l => l.PriorityMode)
            .IsRequired()
            .HasMaxLength(8)
            .HasDefaultValue("auto");

        builder.Property(l => l.CriticalityQ1)
            .HasColumnType("smallint");

        builder.Property(l => l.CriticalityQ2)
            .HasColumnType("smallint");

        builder.Property(l => l.CriticalityQ3)
            .HasColumnType("smallint");

        builder.Property(l => l.CriticalityQ4)
            .HasColumnType("smallint");

        builder.Property(l => l.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(l => l.IsSheddable)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(l => l.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(l => l.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasOne(l => l.Facility)
            .WithMany(f => f.Loads)
            .HasForeignKey(l => l.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Zone)
            .WithMany(z => z.Loads)
            .HasForeignKey(l => l.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.LoadCooldownState)
            .WithOne(lcs => lcs.Load)
            .HasForeignKey<LoadCooldownState>(lcs => lcs.LoadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_loads_power_rating_kw", "power_rating_kw >= 0");
            t.HasCheckConstraint("CK_loads_priority", "priority IN ('P1','P2','P3')");
            t.HasCheckConstraint("CK_loads_priority_mode", "priority_mode IN ('auto','manual')");
            t.HasCheckConstraint("CK_loads_criticality_q1", "criticality_q1 BETWEEN 1 AND 10");
            t.HasCheckConstraint("CK_loads_criticality_q2", "criticality_q2 BETWEEN 1 AND 10");
            t.HasCheckConstraint("CK_loads_criticality_q3", "criticality_q3 BETWEEN 1 AND 10");
            t.HasCheckConstraint("CK_loads_criticality_q4", "criticality_q4 BETWEEN 1 AND 10");
        });

        builder.HasIndex(l => new { l.FacilityId, l.RelayAddress })
            .IsUnique()
            .HasDatabaseName("uq_relay_per_facility");

        builder.HasIndex(l => l.FacilityId)
            .HasDatabaseName("idx_loads_facility");

        builder.HasIndex(l => l.ZoneId)
            .HasDatabaseName("idx_loads_zone");

        builder.HasIndex(l => new { l.FacilityId, l.Priority })
            .HasDatabaseName("idx_loads_priority")
            .HasFilter("is_active = true");
    }
}
