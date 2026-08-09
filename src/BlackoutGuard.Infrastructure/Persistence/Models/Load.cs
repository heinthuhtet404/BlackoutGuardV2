namespace BlackoutGuard.Infrastructure.Persistence.Models;

public class Load
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public Guid ZoneId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int RelayAddress { get; set; }
    public double PowerRatingKw { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string PriorityMode { get; set; } = "auto";
    public short? CriticalityQ1 { get; set; }
    public short? CriticalityQ2 { get; set; }
    public short? CriticalityQ3 { get; set; }
    public short? CriticalityQ4 { get; set; }
    public double? CriticalityScore { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSheddable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Facility Facility { get; set; } = null!;
    public Zone Zone { get; set; } = null!;
    public ICollection<TimeSchedule> TimeSchedules { get; set; } = new List<TimeSchedule>();
    public LoadCooldownState? LoadCooldownState { get; set; }
    public ICollection<DecisionAuditLog> DecisionAuditLogs { get; set; } = new List<DecisionAuditLog>();
}
