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

        // Last Admin Demotion Protection
        if (request.Role is not null && request.Role != "Admin" && user.Role == "Admin")
        {
            var adminCount = await _userRepository.CountAdminsInTenantAsync(tenantId, ct);
            if (adminCount <= 1)
                return Result<UserDto>.Failure("Cannot demote the last Admin in the tenant.");
        }

        // Apply Core Field Updates
        if (!string.IsNullOrWhiteSpace(request.Email))
            user.Email = request.Email;

        if (!string.IsNullOrWhiteSpace(request.Role))
            user.Role = request.Role;

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        // Safe Defaults for Optional Fields
        if (request.FullName is not null)
            user.FullName = string.IsNullOrWhiteSpace(request.FullName) ? string.Empty : request.FullName;

        if (request.FacilityLocation is not null)
            user.FacilityLocation = string.IsNullOrWhiteSpace(request.FacilityLocation) ? string.Empty : request.FacilityLocation;

        if (request.OrganizationName is not null)
            user.OrganizationName = string.IsNullOrWhiteSpace(request.OrganizationName) ? string.Empty : request.OrganizationName;

        // Convert decimal to double
        if (request.GeneratorCapacity.HasValue)
            user.GeneratorCapacity = (double)request.GeneratorCapacity.Value;

        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user, ct);
        await _userRepository.SaveChangesAsync(ct);

        return Result<UserDto>.Success(new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            Role = user.Role ?? string.Empty,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            FullName = user.FullName,
            FacilityLocation = user.FacilityLocation,

            // Fix CS1061: user.GeneratorCapacity က double ဖြစ်နေတဲ့အတွက် (decimal) နဲ့ တိုက်ရိုက် cast လုပ်ပါ
            GeneratorCapacity = (decimal)user.GeneratorCapacity,

            OrganizationName = user.OrganizationName
        });
    }
}