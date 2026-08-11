namespace BlackoutGuard.Application.DTOs;

public class ZoneDto
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid? ParentZoneId { get; set; }
    public List<ZoneDto> Children { get; set; } = new();
}
