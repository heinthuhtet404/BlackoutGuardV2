namespace BlackoutGuard.Application.DTOs;

public class UpdateRuleRequest
{
    public Guid RuleId { get; set; }
    public Guid FacilityId { get; set; }
    public string? Name { get; set; }
    public string? ParameterKey { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public int? CooldownSeconds { get; set; }
    public string? Unit { get; set; }
    public bool? IsActive { get; set; }
}
