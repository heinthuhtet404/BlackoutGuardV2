using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Domain.ValueObjects;

namespace BlackoutGuard.Application.UseCases.Users;

public sealed class ListUsersUseCase
{
    private readonly IUserRepository _userRepository;

    public ListUsersUseCase(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<IReadOnlyList<UserDto>>> ExecuteAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var users = await _userRepository.GetByTenantIdAsync(tenantId, ct);

        var dtos = users.Select(u => new UserDto
        {
            Id = u.Id,
            Email = u.Email,
            Role = u.Role,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt
        }).ToList();

        return Result<IReadOnlyList<UserDto>>.Success(dtos);
    }
}