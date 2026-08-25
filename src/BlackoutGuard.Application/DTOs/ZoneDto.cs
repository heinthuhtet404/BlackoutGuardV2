namespace BlackoutGuard.Application.DTOs;

public class LoadDto
{
    public Guid Id { get; set; }
    public Guid ZoneId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double PowerRatingKw { get; set; }
    public int PriorityLevel { get; set; } = 1;
    public int? RelayAddress { get; set; }
}

public class ZoneDto
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid? ParentZoneId { get; set; }

    // Front-end recursive calculation အတွက် ပါဝင်ရန် လိုအပ်သော Lists
    public List<LoadDto> Loads { get; set; } = new();
    public List<ZoneDto> Children { get; set; } = new();
    public List<ZoneDto> SubZones { get; set; } = new(); // Alias for front-end compatibility
}