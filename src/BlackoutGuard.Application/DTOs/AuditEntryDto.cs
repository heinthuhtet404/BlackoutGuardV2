namespace BlackoutGuard.Application.DTOs;

public class AuditEntryDto
{
    public Guid FacilityId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public Guid? AffectedLoadId { get; set; }
    public double? TriggeringFrequency { get; set; }
    public double? TriggeringVoltage { get; set; }
}
