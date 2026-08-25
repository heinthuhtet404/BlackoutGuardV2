namespace BlackoutGuard.Application.DTOs;

public class LoadDto
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public Guid ZoneId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? RelayAddress { get; set; }
    public double PowerRatingKw { get; set; }

    // Priority properties
    public string Priority { get; set; } = "P1"; // "P1", "P2", "P3"
    public string PriorityMode { get; set; } = "auto";

    // Criticality Assessment Scores
    public short? CriticalityQ1 { get; set; }
    public short? CriticalityQ2 { get; set; }
    public short? CriticalityQ3 { get; set; }
    public short? CriticalityQ4 { get; set; }
    public double? CriticalityScore { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsSheddable { get; set; } = true;

    // Computed level (1, 2, 3) for front-end consumption
    public int PriorityLevel
    {
        get
        {
            if (int.TryParse(Priority, out var parsed)) return parsed;
            if (!string.IsNullOrEmpty(Priority) && Priority.StartsWith("P", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(Priority.Substring(1), out var pNum))
            {
                return pNum;
            }
            return 1; // Default P1
        }
    }
}

public class ZoneDto
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid? ParentZoneId { get; set; }

    // Navigation and Hierarchy Lists
    public List<LoadDto> Loads { get; set; } = new();
    public List<ZoneDto> Children { get; set; } = new();
    public List<ZoneDto> SubZones { get; set; } = new(); // Alias for UI tree mapping
}