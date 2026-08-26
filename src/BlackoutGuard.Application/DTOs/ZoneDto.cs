namespace BlackoutGuard.Application.DTOs;

public class ZoneDto
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "building", "floor", "room"
    public Guid? ParentZoneId { get; set; }

    // Navigation and Hierarchy Lists for Tree Mapping
    public List<LoadDto> Loads { get; set; } = new();
    public List<ZoneDto> Children { get; set; } = new();
    public List<ZoneDto> SubZones { get; set; } = new(); // Alias for front-end compatibility
}