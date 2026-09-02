namespace BlackoutGuard.Application.DTOs;

public class FacilityDto
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double GeneratorCapacityKW { get; set; }
}

public class FacilityConfigResponse
{
    public bool GridOnline { get; set; }
    public double SolarCapacityKw { get; set; }
    public double GeneratorCapacityKw { get; set; }
}