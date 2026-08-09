namespace BlackoutGuard.Infrastructure.Persistence.Models;

public class LoadCooldownState
{
    public Guid LoadId { get; set; }
    public DateTime? LastShedAt { get; set; }
    public DateTime? LastRestoredAt { get; set; }
    public DateTime? CooldownUntil { get; set; }

    public Load Load { get; set; } = null!;
}
