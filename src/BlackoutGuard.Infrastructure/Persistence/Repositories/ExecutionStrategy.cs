using BlackoutGuard.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace BlackoutGuard.Infrastructure.Persistence.Repositories;

public class ExecutionStrategy : IExecutionStrategy
{
    private readonly DbContext _dbContext;

    public ExecutionStrategy(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async (CancellationToken cancellationToken) =>
        {
            return await operation();
        }, ct);
    }
}