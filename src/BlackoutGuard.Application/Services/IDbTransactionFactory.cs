using System.Data;

namespace BlackoutGuard.Application.Services;

public interface IDbTransactionFactory
{
    Task<IDataTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken ct = default);
}
