using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BlackoutGuard.Domain.Entities;

namespace BlackoutGuard.Application.Services;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserAuthDto?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<int> CountAdminsInTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> HasAnyUserAsync(CancellationToken ct = default); // 👈 AUTO-ADMIN စစ်ဆေးရန် ဖြည့်စွက်ထားသည်
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task DeleteAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public class UserAuthDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid FacilityId { get; set; }
}