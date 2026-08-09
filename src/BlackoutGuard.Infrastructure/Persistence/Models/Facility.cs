namespace BlackoutGuard.Infrastructure.Persistence.Models;

public class Facility
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double GeneratorCapacityKW { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
