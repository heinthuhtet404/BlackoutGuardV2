using System.Data;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Schedules;

public class DeleteScheduleUseCase
{
    private readonly IScheduleRepository _scheduleRepo;
    private readonly IDbTransactionFactory _txFactory;
    private readonly IExecutionStrategy _executionStrategy;

    public DeleteScheduleUseCase(
        IScheduleRepository scheduleRepo,
        IDbTransactionFactory txFactory,
        IExecutionStrategy executionStrategy)
    {
        _scheduleRepo = scheduleRepo;
        _txFactory = txFactory;
        _executionStrategy = executionStrategy;
    }

    public async Task<Result> ExecuteAsync(Guid scheduleId, Guid facilityId, CancellationToken ct = default)
    {
        return await _executionStrategy.ExecuteAsync(async () =>
        {
            await using var tx = await _txFactory.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

            try
            {
                var schedule = await _scheduleRepo.GetByIdAsync(scheduleId, facilityId, ct);
                if (schedule is null)
                    return Result.Failure($"Schedule {scheduleId} not found in facility {facilityId}.");

                await _scheduleRepo.DeleteAsync(scheduleId, facilityId, ct);
                await tx.CommitAsync(ct);

                return Result.Success();
            }
            catch (Exception)
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }, ct);
    }
}
