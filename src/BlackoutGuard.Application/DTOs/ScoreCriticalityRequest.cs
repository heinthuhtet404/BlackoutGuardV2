namespace BlackoutGuard.Application.DTOs;

public class ScoreCriticalityRequest
{
    public Guid LoadId { get; set; }
    public Guid FacilityId { get; set; }
    public short Q1 { get; set; }
    public short Q2 { get; set; }
    public short Q3 { get; set; }
    public short Q4 { get; set; }
}
