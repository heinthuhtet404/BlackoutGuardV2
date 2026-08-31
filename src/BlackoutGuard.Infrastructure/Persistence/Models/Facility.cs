using System;
using System.Collections.Generic;

namespace BlackoutGuard.Infrastructure.Persistence.Models;

public class Facility
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double GeneratorCapacityKw { get; set; }

    // ===== NEW COLUMNS FOR STRATEGY A =====
    public double SolarCapacityKw { get; set; } = 0.0;
    public bool IsGridOnline { get; set; } = true;
    // ======================================

    public string TimezoneId { get; set; } = "UTC";
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Tenant? Tenant { get; set; }
    public ICollection<Zone> Zones { get; set; } = new List<Zone>();
    public ICollection<Load> Loads { get; set; } = new List<Load>();
    public ICollection<Rule> Rules { get; set; } = new List<Rule>();
    public ICollection<TimeSchedule> TimeSchedules { get; set; } = new List<TimeSchedule>();
    public ICollection<DecisionAuditLog> DecisionAuditLogs { get; set; } = new List<DecisionAuditLog>();
    public ICollection<AlarmRecord> AlarmRecords { get; set; } = new List<AlarmRecord>();
}