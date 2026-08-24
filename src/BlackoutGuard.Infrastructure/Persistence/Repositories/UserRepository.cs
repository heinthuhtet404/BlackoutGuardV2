using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Domain.Entities;
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

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        return user is null ? null : MapToDomain(user);
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

        return new UserAuthDto
        {
            Id = user.Id,
            TenantId = user.TenantId,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            Role = user.Role,
            FacilityId = facilityId ?? user.TenantId // Facility မရှိသေးပါက TenantId ကို Fallback ပေးသည်
        };
    }

    public async Task<IReadOnlyList<User>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        var users = await _context.Users
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync(ct);

        return users.Select(MapToDomain).ToList();
    }

    public async Task<int> CountAdminsInTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _context.Users
            .CountAsync(u => u.TenantId == tenantId && u.Role == "Admin" && u.IsActive, ct);
    }

    public async Task<bool> HasAnyUserAsync(CancellationToken ct = default)
    {
        return await _context.Users.AnyAsync(ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        var entity = MapToInfrastructure(user);
        await _context.Users.AddAsync(entity, ct);
    }

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        var entity = MapToInfrastructure(user);
        _context.Users.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(User user, CancellationToken ct = default)
    {
        var entity = MapToInfrastructure(user);
        _context.Users.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }

    private static User MapToDomain(Infrastructure.Persistence.Models.User entity)
    {
        return new User
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Email = entity.Email,
            PasswordHash = entity.PasswordHash,
            Role = entity.Role,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static Infrastructure.Persistence.Models.User MapToInfrastructure(User domain)
    {
        return new Infrastructure.Persistence.Models.User
        {
            Id = domain.Id,
            TenantId = domain.TenantId,
            Email = domain.Email,
            PasswordHash = domain.PasswordHash,
            Role = domain.Role,
            IsActive = domain.IsActive,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt
        };
    }
}