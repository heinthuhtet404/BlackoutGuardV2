using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlackoutGuard.Infrastructure.Persistence.Repositories;

public class AuditExportRepository : IAuditExportRepository
{
    private readonly BlackoutGuardDbContext _context;

    public AuditExportRepository(BlackoutGuardDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AuditExportEntry>> GetAuditEntriesAsync(
        Guid facilityId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        var query = _context.DecisionAuditLogs
            .Where(a => a.FacilityId == facilityId);

        if (fromUtc.HasValue)
            query = query.Where(a => a.TimestampUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(a => a.TimestampUtc <= toUtc.Value);

        return await query
            .OrderByDescending(a => a.TimestampUtc)
            .Select(a => new AuditExportEntry
            {
                TimestampUtc = a.TimestampUtc,
                EventType = a.EventType,
                Rationale = a.Rationale,
                AffectedLoadName = a.AffectedLoad != null ? a.AffectedLoad.Name : null,
                AffectedLoadRelayAddress = a.AffectedLoad != null ? a.AffectedLoad.RelayAddress : null
            })
            .ToListAsync(ct);
    }
}
