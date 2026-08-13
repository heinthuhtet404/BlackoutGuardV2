using AppExecutionStrategy = BlackoutGuard.Application.Services.IExecutionStrategy;
using BlackoutGuard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EfExecutionStrategy = Microsoft.EntityFrameworkCore.Storage.IExecutionStrategy;

namespace BlackoutGuard.Infrastructure.Persistence.Repositories;

public class ExecutionStrategy : AppExecutionStrategy
{
    private readonly BlackoutGuardDbContext _dbContext;

    public ExecutionStrategy(BlackoutGuardDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct = default)
    {
        EfExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
            async (CancellationToken cancellationToken) => await operation(),
            ct);
    }
}
