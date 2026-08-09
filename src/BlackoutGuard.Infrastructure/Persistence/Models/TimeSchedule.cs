namespace BlackoutGuard.Infrastructure.Persistence.Models;

public class TimeSchedule
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid LoadId { get; set; }
    public string TargetPriority { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public short[] DaysOfWeek { get; set; } = Array.Empty<short>();
    public bool IsActive { get; set; } = true;

    public Facility Facility { get; set; } = null!;
    public Load Load { get; set; } = null!;
}
