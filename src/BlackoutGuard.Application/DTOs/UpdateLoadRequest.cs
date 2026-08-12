namespace BlackoutGuard.Application.DTOs;

public class UpdateLoadRequest
{
    public Guid LoadId { get; set; }
    public Guid FacilityId { get; set; }
    public string? Name { get; set; }
    public int? RelayAddress { get; set; }
    public double? PowerRatingKw { get; set; }
    public string? Priority { get; set; }
    public string? PriorityMode { get; set; }
    public bool? IsSheddable { get; set; }
    public bool Force { get; set; }
}
