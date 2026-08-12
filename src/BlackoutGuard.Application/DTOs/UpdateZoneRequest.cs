namespace BlackoutGuard.Application.DTOs;

public class UpdateZoneRequest
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public Guid? ParentZoneId { get; set; }
}
