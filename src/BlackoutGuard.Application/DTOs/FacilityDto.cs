namespace BlackoutGuard.Application.DTOs;

public class FacilityDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double GeneratorCapacityKW { get; set; }
}
