namespace BlackoutGuard.Infrastructure.Persistence.Models;

public class Zone
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid? ParentZoneId { get; set; }
    public string MetaData { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Facility Facility { get; set; } = null!;
    public Zone? ParentZone { get; set; }
    public ICollection<Zone> ChildZones { get; set; } = new List<Zone>();
    public ICollection<Load> Loads { get; set; } = new List<Load>();
}
