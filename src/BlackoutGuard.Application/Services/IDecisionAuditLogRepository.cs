using BlackoutGuard.Application.DTOs;

namespace BlackoutGuard.Application.Services;

public interface IDecisionAuditLogRepository
{
    Task AddAsync(AuditEntryDto entry, CancellationToken ct = default);
}
