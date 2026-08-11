using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Infrastructure.Persistence;
using BlackoutGuard.Infrastructure.Persistence.Models;

namespace BlackoutGuard.Infrastructure.Persistence.Repositories;

public class DecisionAuditLogRepository : IDecisionAuditLogRepository
{
    private readonly BlackoutGuardDbContext _context;

    public DecisionAuditLogRepository(BlackoutGuardDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditEntryDto entry, CancellationToken ct = default)
    {
        var entity = new DecisionAuditLog
        {
            FacilityId = entry.FacilityId,
            EventType = entry.EventType,
            Rationale = entry.Rationale,
            AffectedLoadId = entry.AffectedLoadId,
            TriggeringFrequency = entry.TriggeringFrequency,
            TriggeringVoltage = entry.TriggeringVoltage
        };

        _context.DecisionAuditLogs.Add(entity);
        await _context.SaveChangesAsync(ct);
    }
}
