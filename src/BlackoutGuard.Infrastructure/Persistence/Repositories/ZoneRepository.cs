using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Infrastructure.Persistence;
using BlackoutGuard.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BlackoutGuard.Infrastructure.Persistence.Repositories;

public class ZoneRepository : IZoneRepository
{
    private readonly BlackoutGuardDbContext _context;

    public ZoneRepository(BlackoutGuardDbContext context)
    {
        _context = context;
    }

    public async Task<ZoneDto?> GetByIdAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default)
    {
        var zone = await _context.Zones
            .FirstOrDefaultAsync(z => z.Id == zoneId && z.FacilityId == facilityId, ct);

        return zone is null ? null : MapToDto(zone);
    }

    public async Task<Guid> CreateAsync(ZoneDto zone, CancellationToken ct = default)
    {
        var entity = new Zone
        {
            Id = zone.Id,
            FacilityId = zone.FacilityId,
            Name = zone.Name,
            Type = zone.Type,
            ParentZoneId = zone.ParentZoneId
        };

        _context.Zones.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateAsync(ZoneDto zone, CancellationToken ct = default)
    {
        var entity = await _context.Zones.FindAsync([zone.Id], ct);
        if (entity is null) return;

        entity.Name = zone.Name;
        entity.Type = zone.Type;
        entity.ParentZoneId = zone.ParentZoneId;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default)
    {
        var entity = await _context.Zones
            .FirstOrDefaultAsync(z => z.Id == zoneId && z.FacilityId == facilityId, ct);

        if (entity is null) return;

        _context.Zones.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> HasChildrenAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default)
    {
        return await _context.Zones
            .AnyAsync(z => z.ParentZoneId == zoneId && z.FacilityId == facilityId, ct);
    }

    public async Task<bool> HasLoadsAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default)
    {
        return await _context.Loads
            .AnyAsync(l => l.ZoneId == zoneId && l.FacilityId == facilityId, ct);
    }

    public async Task<List<ZoneDto>> GetAllByFacilityAsync(Guid facilityId, CancellationToken ct = default)
    {
        var zones = await _context.Zones
            .Where(z => z.FacilityId == facilityId)
            .AsNoTracking()
            .ToListAsync(ct);

        return zones.Select(MapToDto).ToList();
    }

    public async Task<bool> ExistsInFacilityAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default)
    {
        return await _context.Zones
            .AnyAsync(z => z.Id == zoneId && z.FacilityId == facilityId, ct);
    }

    public async Task<List<Guid>> GetAncestorIdsAsync(Guid zoneId, CancellationToken ct = default)
    {
        var ancestors = new List<Guid>();
        var currentId = zoneId;
        var depth = 0;
        const int maxDepth = 20;

        while (depth < maxDepth)
        {
            var parent = await _context.Zones
                .Where(z => z.Id == currentId)
                .Select(z => z.ParentZoneId)
                .FirstOrDefaultAsync(ct);

            if (parent is null)
                break;

            ancestors.Add(parent.Value);
            currentId = parent.Value;
            depth++;
        }

        return ancestors;
    }

    private static ZoneDto MapToDto(Zone zone)
    {
        return new ZoneDto
        {
            Id = zone.Id,
            FacilityId = zone.FacilityId,
            Name = zone.Name,
            Type = zone.Type,
            ParentZoneId = zone.ParentZoneId
        };
    }
}
