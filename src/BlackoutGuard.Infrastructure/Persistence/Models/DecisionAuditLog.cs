namespace BlackoutGuard.Infrastructure.Persistence.Models;

public class DecisionAuditLog
{
    public long Id { get; set; }
    public Guid FacilityId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string EventType { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public Guid? AffectedLoadId { get; set; }
    public double? TriggeringFrequency { get; set; }
    public double? TriggeringVoltage { get; set; }

    public Facility Facility { get; set; } = null!;
    public Load? AffectedLoad { get; set; }
}
