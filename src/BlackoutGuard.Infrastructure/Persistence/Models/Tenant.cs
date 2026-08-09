namespace BlackoutGuard.Infrastructure.Persistence.Models;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Plan { get; set; } = "trial";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Facility> Facilities { get; set; } = new List<Facility>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
