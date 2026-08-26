using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Exceptions;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Infrastructure.Persistence;
using BlackoutGuard.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlackoutGuard.Infrastructure.Persistence.Repositories;

public class LoadRepository : ILoadRepository
{
    private readonly BlackoutGuardDbContext _context;

    public LoadRepository(BlackoutGuardDbContext context)
    {
        _context = context;
    }

    public async Task<LoadDto?> GetByRelayAddressAsync(Guid facilityId, int relayAddress, Guid? excludeLoadId = null, CancellationToken ct = default)
    {
        var query = _context.Loads
            .Where(l => l.FacilityId == facilityId && l.RelayAddress == relayAddress);

        if (excludeLoadId.HasValue)
            query = query.Where(l => l.Id != excludeLoadId.Value);

        var load = await query.FirstOrDefaultAsync(ct);

        return load is null ? null : MapToDto(load);
    }

    public async Task<LoadDto?> GetByIdAsync(Guid loadId, Guid facilityId, CancellationToken ct = default)
    {
        var load = await _context.Loads
            .FirstOrDefaultAsync(l => l.Id == loadId && l.FacilityId == facilityId, ct);

        return load is null ? null : MapToDto(load);
    }

    public async Task<List<LoadDto>> GetAllByFacilityAsync(Guid facilityId, Guid? zoneId = null, CancellationToken ct = default)
    {
        var query = _context.Loads.Where(l => l.FacilityId == facilityId);

        if (zoneId.HasValue)
            query = query.Where(l => l.ZoneId == zoneId.Value);

        var loads = await query.ToListAsync(ct);
        return loads.Select(MapToDto).ToList();
    }

    public async Task<List<LoadDto>> GetP1LoadsAsync(Guid facilityId, Guid? excludeLoadId = null, CancellationToken ct = default)
    {
        var query = _context.Loads
            .Where(l => l.FacilityId == facilityId && l.Priority == "P1" && l.IsActive);

        if (excludeLoadId.HasValue)
            query = query.Where(l => l.Id != excludeLoadId.Value);

        var loads = await query.ToListAsync(ct);
        return loads.Select(MapToDto).ToList();
    }

    public async Task<Guid> AddAsync(LoadDto load, CancellationToken ct = default)
    {
        var entity = new Load
        {
            Id = load.Id,
            FacilityId = load.FacilityId,
            ZoneId = load.ZoneId,
            Name = load.Name,
            // int? to int conversion fix for Entity
            RelayAddress = load.RelayAddress ?? 0,
            PowerRatingKw = load.PowerRatingKw,
            Priority = load.Priority,
            PriorityMode = load.PriorityMode,
            CriticalityQ1 = load.CriticalityQ1,
            CriticalityQ2 = load.CriticalityQ2,
            CriticalityQ3 = load.CriticalityQ3,
            CriticalityQ4 = load.CriticalityQ4,
            CriticalityScore = load.CriticalityScore,
            IsSheddable = load.IsSheddable,
            IsActive = load.IsActive
        };

        _context.Loads.Add(entity);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx
            && pgEx.SqlState == PostgresErrorCodes.UniqueViolation
            && pgEx.ConstraintName == "uq_relay_per_facility")
        {
            // int? to int explicit conversion fix for Exception
            int relay = load.RelayAddress.HasValue ? load.RelayAddress.Value : 0;
            throw new RelayConflictException(relay, load.Name);
        }

        return entity.Id;
    }

    public async Task UpdateAsync(LoadDto load, CancellationToken ct = default)
    {
        var entity = await _context.Loads
            .FirstOrDefaultAsync(l => l.Id == load.Id && l.FacilityId == load.FacilityId, ct);

        if (entity is null)
            return;

        entity.Name = load.Name;
        // int? to int conversion fix for Entity assignment
        entity.RelayAddress = load.RelayAddress ?? 0;
        entity.PowerRatingKw = load.PowerRatingKw;
        entity.Priority = load.Priority;
        entity.PriorityMode = load.PriorityMode;
        entity.CriticalityQ1 = load.CriticalityQ1;
        entity.CriticalityQ2 = load.CriticalityQ2;
        entity.CriticalityQ3 = load.CriticalityQ3;
        entity.CriticalityQ4 = load.CriticalityQ4;
        entity.CriticalityScore = load.CriticalityScore;
        entity.IsSheddable = load.IsSheddable;
        entity.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx
            && pgEx.SqlState == PostgresErrorCodes.UniqueViolation
            && pgEx.ConstraintName == "uq_relay_per_facility")
        {
            // int? to int explicit conversion fix for Exception
            int relay = load.RelayAddress.HasValue ? load.RelayAddress.Value : 0;
            throw new RelayConflictException(relay, load.Name);
        }
    }

    public async Task DeleteAsync(Guid loadId, Guid facilityId, CancellationToken ct = default)
    {
        var entity = await _context.Loads
            .FirstOrDefaultAsync(l => l.Id == loadId && l.FacilityId == facilityId, ct);

        if (entity is null)
            return;

        _context.Loads.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    private static LoadDto MapToDto(Load load)
    {
        return new LoadDto
        {
            Id = load.Id,
            FacilityId = load.FacilityId,
            ZoneId = load.ZoneId,
            Name = load.Name,
            RelayAddress = load.RelayAddress,
            PowerRatingKw = load.PowerRatingKw,
            Priority = load.Priority,
            PriorityMode = load.PriorityMode,
            CriticalityQ1 = load.CriticalityQ1,
            CriticalityQ2 = load.CriticalityQ2,
            CriticalityQ3 = load.CriticalityQ3,
            CriticalityQ4 = load.CriticalityQ4,
            CriticalityScore = load.CriticalityScore,
            IsActive = load.IsActive,
            IsSheddable = load.IsSheddable
        };
    }
}