namespace BlackoutGuard.Application.DTOs;

public class CreateLoadRequest
{
    public Guid FacilityId { get; set; }
    public Guid ZoneId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int RelayAddress { get; set; }
    public double PowerRatingKw { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string PriorityMode { get; set; } = "auto";
    public bool IsSheddable { get; set; } = true;
    public bool Force { get; set; }

    // Risk factors & Criticality parameters
    public short? CriticalityQ1 { get; set; }
    public short? CriticalityQ2 { get; set; }
    public short? CriticalityQ3 { get; set; }
    public short? CriticalityQ4 { get; set; }
    public double? CriticalityScore { get; set; }
}