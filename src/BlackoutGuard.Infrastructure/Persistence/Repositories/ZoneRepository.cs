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
        // 1. Facility တစ်ခုလုံးရဲ့ Zones နဲ့ direct Loads များကို ဆွဲထုတ်ပါမည်
        var allZones = await _context.Zones
            .Include(z => z.Loads)
            .Where(z => z.FacilityId == facilityId)
            .AsNoTracking()
            .ToListAsync(ct);

        if (!allZones.Any(z => z.Id == zoneId))
            return null;

        var zoneDtos = allZones.Select(MapToDto).ToList();
        var dtoLookup = zoneDtos.ToDictionary(z => z.Id);

        // 2. Hierarchy Tree Build လုပ်ခြင်း
        foreach (var dto in zoneDtos)
        {
            if (dto.ParentZoneId.HasValue && dtoLookup.TryGetValue(dto.ParentZoneId.Value, out var parentDto))
            {
                // Reference Alignment (SubZones ကို Sync လုပ်ပေးထားပါမည်)
                parentDto.Children.Add(dto);
                parentDto.SubZones = parentDto.Children;
            }
        }

        // Requested Zone ID ကို Return ပြန်ပေးပါမည် (Child zones များနှင့် loads များ ပါဝင်ပြီးဖြစ်သည်)
        return dtoLookup.TryGetValue(zoneId, out var targetZone) ? targetZone : null;
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
        // 1. Facility တစ်ခုလုံးရဲ့ Zones နဲ့ direct Loads များကို ဆွဲထုတ်ပါမည်
        var allZones = await _context.Zones
            .Include(z => z.Loads)
            .Where(z => z.FacilityId == facilityId)
            .AsNoTracking()
            .ToListAsync(ct);

        // 2. DTOs အဖြစ် Map လုပ်ပြီး Lookup Table ဆောက်ပါမည်
        var zoneDtos = allZones.Select(MapToDto).ToList();
        var dtoLookup = zoneDtos.ToDictionary(z => z.Id);

        var rootZones = new List<ZoneDto>();

        // 3. Dynamic Hierarchy Tree (Building -> Floor -> Room) တည်ဆောက်ပါမည်
        foreach (var dto in zoneDtos)
        {
            if (dto.ParentZoneId.HasValue && dtoLookup.TryGetValue(dto.ParentZoneId.Value, out var parentDto))
            {
                parentDto.Children.Add(dto);
                parentDto.SubZones = parentDto.Children; // Same reference point for SubZones
            }
            else
            {
                // ParentZoneId လုံးဝ မရှိသူ သို့မဟုတ် ParentZoneId ရှိသော်လည်း Parent DB ထဲ မရှိသူများကို Root အဖြစ် သတ်မှတ်ပါမည်
                rootZones.Add(dto);
            }
        }

        return rootZones;
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
        var childrenList = new List<ZoneDto>();

        return new ZoneDto
        {
            Id = zone.Id,
            FacilityId = zone.FacilityId,
            Name = zone.Name,
            Type = zone.Type,
            ParentZoneId = zone.ParentZoneId,
            Loads = zone.Loads?.Select(l => new LoadDto
            {
                Id = l.Id,
                FacilityId = l.FacilityId,
                ZoneId = l.ZoneId,
                Name = l.Name,
                RelayAddress = l.RelayAddress,
                PowerRatingKw = l.PowerRatingKw,
                Priority = l.Priority,
                PriorityMode = l.PriorityMode,
                CriticalityQ1 = l.CriticalityQ1,
                CriticalityQ2 = l.CriticalityQ2,
                CriticalityQ3 = l.CriticalityQ3,
                CriticalityQ4 = l.CriticalityQ4,
                CriticalityScore = l.CriticalityScore,
                IsActive = l.IsActive,
                IsSheddable = l.IsSheddable
            }).ToList() ?? new List<LoadDto>(),
            Children = childrenList,
            SubZones = childrenList // Alias reference sharing
        };
    }
}