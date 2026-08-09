namespace BlackoutGuard.Infrastructure.Persistence.Models;

public class AlarmRecord
{
    public long Id { get; set; }
    public Guid FacilityId { get; set; }
    public string AlarmCode { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAtUtc { get; set; }
    public Guid? AcknowledgedBy { get; set; }

    public Facility Facility { get; set; } = null!;
    public User? AcknowledgedByUser { get; set; }
}
