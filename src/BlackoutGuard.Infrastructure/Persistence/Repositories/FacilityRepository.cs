using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlackoutGuard.Infrastructure.Persistence.Repositories;

public class FacilityRepository : IFacilityRepository
{
    private readonly BlackoutGuardDbContext _context;

    public FacilityRepository(BlackoutGuardDbContext context)
    {
        _context = context;
    }

    public async Task<FacilityDto?> GetByIdAsync(Guid facilityId, CancellationToken ct = default)
    {
        var facility = await _context.Facilities
            .FirstOrDefaultAsync(f => f.Id == facilityId, ct);

        return facility is null ? null : new FacilityDto
        {
            Id = facility.Id,
            TenantId = facility.TenantId,
            Name = facility.Name,
            GeneratorCapacityKW = facility.GeneratorCapacityKw
        };
    }

    // ဒီ Method ကို ထပ်ဖြည့်ပေးပါ
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Facilities
            .AnyAsync(f => f.Id == id, cancellationToken);
    }
}