using System.Data;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BlackoutGuard.Infrastructure.Persistence.Repositories;

public class DbTransactionFactory : IDbTransactionFactory
{
    private readonly BlackoutGuardDbContext _context;

    public DbTransactionFactory(BlackoutGuardDbContext context)
    {
        _context = context;
    }

    public async Task<IDataTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.RepeatableRead, CancellationToken ct = default)
    {
        var efTx = await _context.Database.BeginTransactionAsync(isolationLevel, ct);
        return new DbTransactionWrapper(efTx);
    }
}

internal sealed class DbTransactionWrapper : IDataTransaction
{
    private readonly IDbContextTransaction _efTx;

    public DbTransactionWrapper(IDbContextTransaction efTx)
    {
        _efTx = efTx;
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        await _efTx.CommitAsync(ct);
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        await _efTx.RollbackAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _efTx.DisposeAsync();
    }
}
