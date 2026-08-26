using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlackoutGuard.Application.UseCases.Shedding;

public class EvaluateSheddingUseCase
{
    private readonly ILoadRepository _loadRepo;
    private readonly IFacilityRepository _facilityRepo;

    public EvaluateSheddingUseCase(ILoadRepository loadRepo, IFacilityRepository facilityRepo)
    {
        _loadRepo = loadRepo;
        _facilityRepo = facilityRepo;
    }

    public async Task<Result<SheddingRecommendationDto>> ExecuteAsync(Guid facilityId, double availableCapacityKw, CancellationToken ct = default)
    {
        var loads = await _loadRepo.GetAllByFacilityAsync(facilityId, null, ct);
        var activeLoads = loads.Where(l => l.IsActive).ToList();

        double totalDemandKw = activeLoads.Sum(l => l.PowerRatingKw);
        double deficitKw = totalDemandKw - availableCapacityKw;

        var recommendation = new SheddingRecommendationDto
        {
            FacilityId = facilityId,
            TotalCapacityKw = availableCapacityKw,
            CurrentDemandKw = totalDemandKw,
            PowerDeficitKw = deficitKw > 0 ? deficitKw : 0
        };

        // Power ကျေလောက်အောင် ရှိနေပါက Shed လုပ်ရန် မလိုပါ
        if (deficitKw <= 0)
        {
            recommendation.ActiveLoadsRemaining = activeLoads;
            recommendation.ExpectedDemandAfterShedKw = totalDemandKw;
            return Result<SheddingRecommendationDto>.Success(recommendation);
        }

        // Sheddable ဖြစ်သော Load များကို Priority Ascending (P3 -> P2 -> P1) နဲ့ Criticality Score Ascending အတိုင်း Sort လုပ်မည်
        var candidateLoads = activeLoads
            .Where(l => l.IsSheddable)
            .OrderBy(l => GetPriorityRank(l.Priority))
            .ThenBy(l => l.CriticalityScore ?? 0)
            .ToList();

        double currentDeficitToClear = deficitKw;
        var remainingLoads = activeLoads.ToList();

        foreach (var load in candidateLoads)
        {
            if (currentDeficitToClear <= 0)
                break;

            recommendation.LoadsToShed.Add(new LoadSheddingActionDto
            {
                LoadId = load.Id,
                LoadName = load.Name,
                // RelayAddress က int? (Nullable) ဖြစ်နေသဖြင့် int သို့ မပြောင်းမီ Null Check / Fallback ထည့်သွင်းထားသည်
                RelayAddress = load.RelayAddress ?? 0,
                PowerRatingKw = load.PowerRatingKw,
                Priority = load.Priority,
                CriticalityScore = load.CriticalityScore,
                Reason = $"Priority {load.Priority} shed to reduce {load.PowerRatingKw} kW deficit (Score: {load.CriticalityScore:F2})"
            });

            currentDeficitToClear -= load.PowerRatingKw;
            remainingLoads.RemoveAll(l => l.Id == load.Id);
        }

        recommendation.ActiveLoadsRemaining = remainingLoads;
        recommendation.ExpectedDemandAfterShedKw = remainingLoads.Sum(l => l.PowerRatingKw);

        return Result<SheddingRecommendationDto>.Success(recommendation);
    }

    private static int GetPriorityRank(string priority) => priority.ToUpper() switch
    {
        "P3" => 1,
        "P2" => 2,
        "P1" => 3,
        _ => 1
    };
}