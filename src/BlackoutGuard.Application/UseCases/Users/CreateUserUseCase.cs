using System;
using System.Threading;
using System.Threading.Tasks;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Domain.ValueObjects;

namespace BlackoutGuard.Application.UseCases.Users;

public sealed class CreateUserUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public CreateUserUseCase(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<UserDto>> ExecuteAsync(
        Guid tenantId,
        string email,
        string password,
        string role,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result<UserDto>.Failure("Email is required.");

        var existing = await _userRepository.GetByEmailAsync(email, ct);
        if (existing is not null)
            return Result<UserDto>.Failure("User with this email already exists.");

        if (role != "Admin" && role != "Operator" && role != "Viewer")
            return Result<UserDto>.Failure("Invalid role. Must be Admin, Operator, or Viewer.");

        var passwordHash = _passwordHasher.Hash(password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, ct);
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