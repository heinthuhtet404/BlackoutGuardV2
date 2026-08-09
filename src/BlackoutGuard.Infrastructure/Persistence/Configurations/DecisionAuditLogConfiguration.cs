using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackoutGuard.Infrastructure.Persistence.Models;

namespace BlackoutGuard.Infrastructure.Persistence.Configurations;

public class DecisionAuditLogConfiguration : IEntityTypeConfiguration<DecisionAuditLog>
{
    public void Configure(EntityTypeBuilder<DecisionAuditLog> builder)
    {
        builder.ToTable("decision_audit_log");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .UseIdentityColumn();

        builder.Property(d => d.EventType)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(d => d.Rationale)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(d => d.TimestampUtc)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasOne(d => d.Facility)
            .WithMany(f => f.DecisionAuditLogs)
            .HasForeignKey(d => d.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.AffectedLoad)
            .WithMany(l => l.DecisionAuditLogs)
            .HasForeignKey(d => d.AffectedLoadId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(d => new { d.FacilityId, d.TimestampUtc })
            .IsDescending(false, true)
            .HasDatabaseName("idx_audit_facility_time");
    }
}
