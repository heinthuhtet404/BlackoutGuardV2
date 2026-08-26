using System;

namespace BlackoutGuard.Application.DTOs;

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    // Optional / Extra Fields
    public string? FullName { get; set; }
    public string? FacilityLocation { get; set; }
    public decimal? GeneratorCapacity { get; set; }
    public string? OrganizationName { get; set; }
}

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Operator";

    // Optional Fields for Creation
    public string? FullName { get; set; }
    public string? FacilityLocation { get; set; }
    public decimal? GeneratorCapacity { get; set; }
    public string? OrganizationName { get; set; }
}

public class UpdateUserRequest
{
    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }

    // Optional Fields for Update
    public string? FullName { get; set; }
    public string? FacilityLocation { get; set; }
    public decimal? GeneratorCapacity { get; set; }
    public string? OrganizationName { get; set; }
}