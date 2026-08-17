using BlackoutGuard.Application.DTOs;

namespace BlackoutGuard.Application.Services;

public interface IAuditExportRepository
{
    Task<IReadOnlyList<AuditExportEntry>> GetAuditEntriesAsync(
        Guid facilityId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default);
}
