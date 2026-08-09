using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackoutGuard.Infrastructure.Persistence.Models;

namespace BlackoutGuard.Infrastructure.Persistence.Configurations;

public class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        builder.ToTable("zones");

        builder.HasKey(z => z.Id);

        builder.Property(z => z.Id)
            .ValueGeneratedOnAdd();

        builder.Property(z => z.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(z => z.Type)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(z => z.MetaData)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(z => z.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(z => z.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasOne(z => z.Facility)
            .WithMany(f => f.Zones)
            .HasForeignKey(z => z.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(z => z.ParentZone)
            .WithMany(z => z.ChildZones)
            .HasForeignKey(z => z.ParentZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint("CK_zones_type", "type IN ('building','floor','room')"));

        builder.HasIndex(z => z.FacilityId)
            .HasDatabaseName("idx_zones_facility");

        builder.HasIndex(z => z.ParentZoneId)
            .HasDatabaseName("idx_zones_parent");
    }
}
