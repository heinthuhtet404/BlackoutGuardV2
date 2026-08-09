namespace BlackoutGuard.Infrastructure.Persistence.Models;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Plan { get; set; } = "trial";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
