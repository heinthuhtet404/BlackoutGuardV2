namespace BlackoutGuard.Application.DTOs;

public class CreateZoneRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid? ParentZoneId { get; set; }
}
