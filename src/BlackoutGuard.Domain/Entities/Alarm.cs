namespace BlackoutGuard.Domain.Entities;

public class Alarm
{
    public Guid Id { get; set; }
    public string AlarmCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning"; // Info, Warning, Critical
    public string State { get; set; } = "Active"; // Active, Acknowledged, Cleared
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAtUtc { get; set; }
    public DateTime? ClearedAtUtc { get; set; }

    public static Alarm CreateCritical(string code, string message)
    {
        return new Alarm
        {
            Id = Guid.NewGuid(),
            AlarmCode = code,
            Message = message,
            Severity = "Critical",
            State = "Active",
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public static Alarm CreateWarning(string code, string message)
    {
        return new Alarm
        {
            Id = Guid.NewGuid(),
            AlarmCode = code,
            Message = message,
            Severity = "Warning",
            State = "Active",
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void Acknowledge()
    {
        State = "Acknowledged";
        AcknowledgedAtUtc = DateTime.UtcNow;
    }

    public void Clear()
    {
        State = "Cleared";
        ClearedAtUtc = DateTime.UtcNow;
    }
}