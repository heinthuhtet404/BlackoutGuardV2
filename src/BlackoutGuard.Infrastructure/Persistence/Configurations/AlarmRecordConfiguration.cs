using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackoutGuard.Infrastructure.Persistence.Models;

namespace BlackoutGuard.Infrastructure.Persistence.Configurations;

public class AlarmRecordConfiguration : IEntityTypeConfiguration<AlarmRecord>
{
    public void Configure(EntityTypeBuilder<AlarmRecord> builder)
    {
        builder.ToTable("alarm_records");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .UseIdentityColumn();

        builder.Property(a => a.AlarmCode)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(a => a.Severity)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(a => a.State)
            .IsRequired()
            .HasMaxLength(24);

        builder.Property(a => a.Message)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(a => a.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasOne(a => a.Facility)
            .WithMany(f => f.AlarmRecords)
            .HasForeignKey(a => a.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.AcknowledgedByUser)
            .WithMany(u => u.AcknowledgedAlarms)
            .HasForeignKey(a => a.AcknowledgedBy)
            .OnDelete(DeleteBehavior.NoAction);

        builder.ToTable(t => t.HasCheckConstraint("CK_alarm_records_severity", "severity IN ('Info','Warning','Critical')"));

        builder.HasIndex(a => new { a.FacilityId, a.State })
            .HasDatabaseName("idx_alarms_facility_state");
    }
}
