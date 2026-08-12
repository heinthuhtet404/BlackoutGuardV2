using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Infrastructure.Persistence;
using BlackoutGuard.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BlackoutGuard.Infrastructure.Persistence.Repositories;

public class RuleRepository : IRuleRepository
{
    private readonly BlackoutGuardDbContext _context;

    public RuleRepository(BlackoutGuardDbContext context)
    {
        _context = context;
    }

    public async Task<List<RuleDto>> GetAllByFacilityAsync(Guid facilityId, CancellationToken ct = default)
    {
        return await _context.Rules
            .Where(r => r.FacilityId == facilityId)
            .Select(r => MapToDto(r))
            .ToListAsync(ct);
    }

    public async Task<RuleDto?> GetByIdAsync(Guid ruleId, Guid facilityId, CancellationToken ct = default)
    {
        var rule = await _context.Rules
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.FacilityId == facilityId, ct);

        return rule is null ? null : MapToDto(rule);
    }

    public async Task UpdateAsync(RuleDto rule, CancellationToken ct = default)
    {
        var entity = await _context.Rules
            .FirstOrDefaultAsync(r => r.Id == rule.Id && r.FacilityId == rule.FacilityId, ct);

        if (entity is null)
            return;

        entity.Name = rule.Name;
        entity.ParameterKey = rule.ParameterKey;
        entity.MinValue = rule.MinValue;
        entity.MaxValue = rule.MaxValue;
        entity.CooldownSeconds = rule.CooldownSeconds;
        entity.Unit = rule.Unit;
        entity.IsActive = rule.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }

    private static RuleDto MapToDto(Rule rule)
    {
        return new RuleDto
        {
            Id = rule.Id,
            FacilityId = rule.FacilityId,
            Name = rule.Name,
            ParameterKey = rule.ParameterKey,
            MinValue = rule.MinValue,
            MaxValue = rule.MaxValue,
            CooldownSeconds = rule.CooldownSeconds,
            Unit = rule.Unit,
            IsActive = rule.IsActive
        };
    }
}
