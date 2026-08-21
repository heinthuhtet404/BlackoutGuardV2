using System;
using System.Threading;
using System.Threading.Tasks;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Domain.ValueObjects;

namespace BlackoutGuard.Application.UseCases.Users;

public sealed class UpdateUserUseCase
{
    private readonly IUserRepository _userRepository;

    public UpdateUserUseCase(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<UserDto>> ExecuteAsync(
        Guid tenantId,
        Guid userId,
        UpdateUserRequest request,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
            return Result<UserDto>.Failure("User not found.");

        if (user.TenantId != tenantId)
            return Result<UserDto>.Failure("User not found in this tenant.");

        if (request.Role is not null && request.Role != "Admin" && user.Role == "Admin")
        {
            var adminCount = await _userRepository.CountAdminsInTenantAsync(tenantId, ct);
            if (adminCount <= 1)
                return Result<UserDto>.Failure("Cannot demote the last Admin in the tenant.");
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
            user.Email = request.Email;

        if (!string.IsNullOrWhiteSpace(request.Role))
            user.Role = request.Role;

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user, ct);
        await _userRepository.SaveChangesAsync(ct);

        return Result<UserDto>.Success(new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        });
    }
}