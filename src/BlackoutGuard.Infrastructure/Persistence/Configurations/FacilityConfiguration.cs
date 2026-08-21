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

        builder.Property(f => f.GeneratorCapacityKw)
            .IsRequired();

        // 👇 NEW: TimezoneId column
        builder.Property(f => f.TimezoneId)
            .IsRequired()
            .HasMaxLength(64)
            .HasDefaultValue("UTC");

        builder.Property(f => f.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasOne(f => f.Tenant)
            .WithMany(t => t.Facilities)
            .HasForeignKey(f => f.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.TenantId)
            .HasDatabaseName("idx_facilities_tenant");
    }
}