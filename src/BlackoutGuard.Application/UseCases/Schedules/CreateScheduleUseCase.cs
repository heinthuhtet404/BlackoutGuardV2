using System.Data;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Schedules;

public class CreateScheduleUseCase
{
    private static readonly string[] ValidPriorities = { "P1", "P2", "P3" };

    private readonly IScheduleRepository _scheduleRepo;
    private readonly ILoadRepository _loadRepo;
    private readonly IDbTransactionFactory _txFactory;
    private readonly IExecutionStrategy _executionStrategy;

    public CreateScheduleUseCase(
        IScheduleRepository scheduleRepo,
        ILoadRepository loadRepo,
        IDbTransactionFactory txFactory,
        IExecutionStrategy executionStrategy)
    {
        _scheduleRepo = scheduleRepo;
        _loadRepo = loadRepo;
        _txFactory = txFactory;
        _executionStrategy = executionStrategy;
    }

    public async Task<Result<Guid>> ExecuteAsync(CreateScheduleRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<Guid>.Failure("Schedule name is required.");

        if (!ValidPriorities.Contains(request.TargetPriority))
            return Result<Guid>.Failure(
                $"Invalid target_priority '{request.TargetPriority}'. Must be one of: P1, P2, P3.");

        if (request.DaysOfWeek is null || request.DaysOfWeek.Length == 0)
            return Result<Guid>.Failure("days_of_week must contain at least one day (1-7).");

        if (request.DaysOfWeek.Any(d => d is < 1 or > 7))
            return Result<Guid>.Failure("days_of_week values must be between 1 (Monday) and 7 (Sunday).");

        if (request.DaysOfWeek.Distinct().Count() != request.DaysOfWeek.Length)
            return Result<Guid>.Failure("days_of_week must not contain duplicate days.");

        return await _executionStrategy.ExecuteAsync(async () =>
        {
            await using var tx = await _txFactory.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

            try
            {
                var load = await _loadRepo.GetByIdAsync(request.LoadId, request.FacilityId, ct);
                if (load is null)
                    return Result<Guid>.Failure(
                        $"Load {request.LoadId} not found in facility {request.FacilityId}.");

                var scheduleId = Guid.NewGuid();
                var schedule = new ScheduleDto
                {
                    Id = scheduleId,
                    FacilityId = request.FacilityId,
                    Name = request.Name,
                    LoadId = request.LoadId,
                    TargetPriority = request.TargetPriority,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    DaysOfWeek = request.DaysOfWeek,
                    IsActive = true
                };

                await _scheduleRepo.CreateAsync(schedule, ct);
                await tx.CommitAsync(ct);

                return Result<Guid>.Success(scheduleId);
            }
            catch (Exception)
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }, ct);
    }
}
