namespace BlackoutGuard.Application.DTOs;

public class RuleDto
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ParameterKey { get; set; } = string.Empty;
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public int CooldownSeconds { get; set; } = 30;
    public string Unit { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
