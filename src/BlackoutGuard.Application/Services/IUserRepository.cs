namespace BlackoutGuard.Application.Services;

public interface IUserRepository
{
    Task<UserAuthDto?> GetByEmailAsync(string email, CancellationToken ct = default);
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
