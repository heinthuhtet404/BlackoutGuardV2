namespace BlackoutGuard.Application.DTOs;

public class CreateScheduleRequest
{
    public Guid FacilityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid LoadId { get; set; }
    public string TargetPriority { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public short[] DaysOfWeek { get; set; } = Array.Empty<short>();
}
