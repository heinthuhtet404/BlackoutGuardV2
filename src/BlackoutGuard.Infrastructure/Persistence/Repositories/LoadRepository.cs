using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Exceptions;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Infrastructure.Persistence;
using BlackoutGuard.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BlackoutGuard.Infrastructure.Persistence.Repositories;

public class LoadRepository : ILoadRepository
{
    private readonly BlackoutGuardDbContext _context;

    public LoadRepository(BlackoutGuardDbContext context)
    {
        _context = context;
    }

    public async Task<LoadDto?> GetByRelayAddressAsync(Guid facilityId, int relayAddress, CancellationToken ct = default)
    {
        var load = await _context.Loads
            .FirstOrDefaultAsync(l => l.FacilityId == facilityId && l.RelayAddress == relayAddress, ct);

        return load is null ? null : MapToDto(load);
    }

    public async Task<List<LoadDto>> GetP1LoadsAsync(Guid facilityId, CancellationToken ct = default)
    {
        return await _context.Loads
            .Where(l => l.FacilityId == facilityId && l.Priority == "P1" && l.IsActive)
            .Select(l => MapToDto(l))
            .ToListAsync(ct);
    }

    public async Task<Guid> AddAsync(LoadDto load, CancellationToken ct = default)
    {
        var entity = new Load
        {
            Id = load.Id,
            FacilityId = load.FacilityId,
            ZoneId = load.ZoneId,
            Name = load.Name,
            RelayAddress = load.RelayAddress,
            PowerRatingKw = load.PowerRatingKw,
            Priority = load.Priority,
            PriorityMode = load.PriorityMode,
            IsSheddable = load.IsSheddable
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
            throw new RelayConflictException(load.RelayAddress, load.Name);
        }

        return entity.Id;
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
            IsActive = load.IsActive,
            IsSheddable = load.IsSheddable
        };
    }
}
