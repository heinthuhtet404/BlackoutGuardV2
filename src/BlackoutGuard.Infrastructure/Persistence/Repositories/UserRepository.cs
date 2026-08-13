using BlackoutGuard.Application.Services;
using BlackoutGuard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlackoutGuard.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly BlackoutGuardDbContext _context;

    public UserRepository(BlackoutGuardDbContext context)
    {
        _context = context;
    }

    public async Task<UserAuthDto?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
            return null;

        var facilityId = await _context.Facilities
            .Where(f => f.TenantId == user.TenantId)
            .Select(f => (Guid?)f.Id)
            .FirstOrDefaultAsync(ct);

        if (facilityId is null)
            return null;

        return new UserAuthDto
        {
            Id = user.Id,
            TenantId = user.TenantId,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            Role = user.Role,
            FacilityId = facilityId.Value
        };
    }
}
