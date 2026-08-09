using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackoutGuard.Infrastructure.Persistence.Models;

namespace BlackoutGuard.Infrastructure.Persistence.Configurations;

public class RuleConfiguration : IEntityTypeConfiguration<Rule>
{
    public void Configure(EntityTypeBuilder<Rule> builder)
    {
        builder.ToTable("rules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedOnAdd();

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(r => r.ParameterKey)
            .IsRequired()
            .HasMaxLength(24);

        builder.Property(r => r.MinValue)
            .IsRequired();

        builder.Property(r => r.MaxValue)
            .IsRequired();

        builder.Property(r => r.CooldownSeconds)
            .IsRequired()
            .HasDefaultValue(30);

        builder.Property(r => r.Unit)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(r => r.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(r => r.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasOne(r => r.Facility)
            .WithMany(f => f.Rules)
            .HasForeignKey(r => r.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_rules_parameter_key", "parameter_key IN ('FREQ_LOW','FREQ_HIGH','VOLT_LOW','VOLT_HIGH','LOAD_SHED_TIMER')");
            t.HasCheckConstraint("CK_rules_cooldown_seconds", "cooldown_seconds >= 0");
            t.HasCheckConstraint("CK_rules_value_order", "min_value <= max_value");
        });

        builder.HasIndex(r => r.FacilityId)
            .HasDatabaseName("idx_rules_facility");
    }
}
