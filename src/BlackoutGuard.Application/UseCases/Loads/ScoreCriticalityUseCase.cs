using System.Data;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Loads;

public class ScoreCriticalityUseCase
{
    private readonly ILoadRepository _loadRepo;
    private readonly IFacilityRepository _facilityRepo;
    private readonly IDbTransactionFactory _txFactory;
    private readonly IExecutionStrategy _executionStrategy;
    private readonly LoadSafetyGuard _safetyGuard;

    public ScoreCriticalityUseCase(
        ILoadRepository loadRepo,
        IFacilityRepository facilityRepo,
        IDbTransactionFactory txFactory,
        IExecutionStrategy executionStrategy)
    {
        _loadRepo = loadRepo;
        _facilityRepo = facilityRepo;
        _txFactory = txFactory;
        _executionStrategy = executionStrategy;
        _safetyGuard = new LoadSafetyGuard(loadRepo, facilityRepo);
    }

    public async Task<Result<ScoredCriticality>> ExecuteAsync(ScoreCriticalityRequest request, CancellationToken ct = default)
    {
        if (request.Q1 is < 1 or > 10 || request.Q2 is < 1 or > 10 ||
            request.Q3 is < 1 or > 10 || request.Q4 is < 1 or > 10)
            return Result<ScoredCriticality>.Failure("All criticality inputs (q1-q4) must be between 1 and 10.");

        return await _executionStrategy.ExecuteAsync(async () =>
        {
            await using var tx = await _txFactory.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

            try
            {
                var load = await _loadRepo.GetByIdAsync(request.LoadId, request.FacilityId, ct);
                if (load is null)
                    return Result<ScoredCriticality>.Failure($"Load {request.LoadId} not found in facility {request.FacilityId}.");

                if (load.PriorityMode != "auto")
                    return Result<ScoredCriticality>.Failure(
                        $"Load '{load.Name}' is in manual priority mode. Manual-priority loads cannot be auto-scored.");

                var rawScore = (request.Q1 * 0.5) + (request.Q2 * 0.3) + (request.Q3 * 0.2);
                var score = rawScore * 10;

                var newPriority = score switch
                {
                    >= 80 => "P1",
                    >= 40 => "P2",
                    _ => "P3"
                };

                if (load.IsActive && load.Priority != newPriority && newPriority == "P1")
                {
                    var capacity = await _safetyGuard.EvaluateCapacityAsync(request.FacilityId, load.PowerRatingKw, request.LoadId, ct);
                    if (capacity.Facility is null)
                        return Result<ScoredCriticality>.Failure($"Facility {request.FacilityId} not found.");

                    if (capacity.Deficit > 0)
                    {
                        return Result<ScoredCriticality>.Failure(
                            $"P1 capacity exceeded by {capacity.Deficit:F1} kW. " +
                            $"Total P1: {capacity.TotalP1Kw:F1} kW, Capacity: {capacity.Facility.GeneratorCapacityKW:F1} kW.");
                    }
                }

                load.CriticalityQ1 = request.Q1;
                load.CriticalityQ2 = request.Q2;
                load.CriticalityQ3 = request.Q3;
                load.CriticalityQ4 = request.Q4;
                load.CriticalityScore = score;
                load.Priority = newPriority;

                await _loadRepo.UpdateAsync(load, ct);
                await tx.CommitAsync(ct);

                return Result<ScoredCriticality>.Success(new ScoredCriticality(score, newPriority));
            }
            catch (Exception)
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }, ct);
    }
}

public sealed record ScoredCriticality(double Score, string Priority);
