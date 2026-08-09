using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackoutGuard.Infrastructure.Persistence.Models;

namespace BlackoutGuard.Infrastructure.Persistence.Configurations;

public class LoadCooldownStateConfiguration : IEntityTypeConfiguration<LoadCooldownState>
{
    public void Configure(EntityTypeBuilder<LoadCooldownState> builder)
    {
        builder.ToTable("load_cooldown_state");

        builder.HasKey(lcs => lcs.LoadId);

        builder.Property(lcs => lcs.LoadId)
            .ValueGeneratedNever();
    }
}
