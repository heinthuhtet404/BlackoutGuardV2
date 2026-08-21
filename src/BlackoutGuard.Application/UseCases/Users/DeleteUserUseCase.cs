using System;
using System.Threading;
using System.Threading.Tasks;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Domain.ValueObjects;

namespace BlackoutGuard.Application.UseCases.Users;

public sealed class DeleteUserUseCase
{
    private readonly IUserRepository _userRepository;

    public DeleteUserUseCase(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<bool>> ExecuteAsync(
        Guid tenantId,
        Guid userId,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
            return Result<bool>.Failure("User not found.");

        if (user.TenantId != tenantId)
            return Result<bool>.Failure("User not found in this tenant.");

        if (userId == currentUserId)
            return Result<bool>.Failure("You cannot delete your own account.");

        if (user.Role == "Admin")
        {
            var adminCount = await _userRepository.CountAdminsInTenantAsync(tenantId, ct);
            if (adminCount <= 1)
                return Result<bool>.Failure("Cannot delete the last Admin in the tenant.");
        }

        await _userRepository.DeleteAsync(user, ct);
        await _userRepository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}