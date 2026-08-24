using System;
using System.Collections.Generic;

namespace BlackoutGuard.Infrastructure.Persistence.Models;

public class User
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;         // 👈 ဖြည့်စွက်ထားပါသည်
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty; // 👈 ဖြည့်စွက်ထားပါသည်
    public double GeneratorCapacity { get; set; }               // 👈 ဖြည့်စွက်ထားပါသည်
    public string? FacilityLocation { get; set; }                // 👈 ဖြည့်စွက်ထားပါသည်
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation Properties
    public Tenant Tenant { get; set; } = null!;
    public ICollection<AlarmRecord> AcknowledgedAlarms { get; set; } = new List<AlarmRecord>();
}