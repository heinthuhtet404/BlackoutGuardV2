using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackoutGuard.Infrastructure.Persistence.Models;

namespace BlackoutGuard.Infrastructure.Persistence.Configurations;

public class TimeScheduleConfiguration : IEntityTypeConfiguration<TimeSchedule>
{
    public void Configure(EntityTypeBuilder<TimeSchedule> builder)
    {
        builder.ToTable("time_schedules");

        builder.HasKey(ts => ts.Id);

        builder.Property(ts => ts.Id)
            .ValueGeneratedOnAdd();

        builder.Property(ts => ts.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(ts => ts.TargetPriority)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(ts => ts.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne(ts => ts.Facility)
            .WithMany(f => f.TimeSchedules)
            .HasForeignKey(ts => ts.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ts => ts.Load)
            .WithMany(l => l.TimeSchedules)
            .HasForeignKey(ts => ts.LoadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t => t.HasCheckConstraint("CK_time_schedules_target_priority", "target_priority IN ('P1','P2','P3')"));

        builder.HasIndex(ts => ts.LoadId)
            .HasDatabaseName("idx_schedules_load");

        builder.HasIndex(ts => ts.FacilityId)
            .HasDatabaseName("idx_schedules_facility");
    }
}
