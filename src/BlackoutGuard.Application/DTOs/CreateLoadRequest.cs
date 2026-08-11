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
}
