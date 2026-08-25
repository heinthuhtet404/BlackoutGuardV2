using System;
using System.Collections.Generic;

namespace BlackoutGuard.Application.DTOs;

public class SheddingRecommendationDto
{
    public Guid FacilityId { get; set; }
    public double TotalCapacityKw { get; set; }
    public double CurrentDemandKw { get; set; }
    public double PowerDeficitKw { get; set; }
    public double ExpectedDemandAfterShedKw { get; set; }

    public List<LoadSheddingActionDto> LoadsToShed { get; set; } = new();
    public List<LoadDto> ActiveLoadsRemaining { get; set; } = new();
}

public class LoadSheddingActionDto
{
    public Guid LoadId { get; set; }
    public string LoadName { get; set; } = string.Empty;
    public int RelayAddress { get; set; }
    public double PowerRatingKw { get; set; }
    public string Priority { get; set; } = string.Empty;
    public double? CriticalityScore { get; set; }
    public string Reason { get; set; } = string.Empty;
}