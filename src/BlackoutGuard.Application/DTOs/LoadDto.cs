namespace BlackoutGuard.Application.DTOs;

public class LoadDto
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public Guid ZoneId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? RelayAddress { get; set; }
    public double PowerRatingKw { get; set; }

    // Priority Properties
    public string Priority { get; set; } = "P3"; // Default P3
    public string PriorityMode { get; set; } = "auto";

    // Criticality Assessment / Risk Scores (Mapped from Frontend)
    public int SafetyRisk { get; set; } = 5;
    public int DataLossRisk { get; set; } = 5;
    public int OperationalRisk { get; set; } = 5;
    public int ComfortRisk { get; set; } = 5;

    // Legacy Question-based Scores
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
            return 3;
        }
    }
}