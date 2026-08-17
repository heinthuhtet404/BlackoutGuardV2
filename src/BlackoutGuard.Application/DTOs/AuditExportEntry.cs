namespace BlackoutGuard.Application.DTOs;

public class AuditExportEntry
{
    public DateTime TimestampUtc { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string? AffectedLoadName { get; set; }
    public int? AffectedLoadRelayAddress { get; set; }
}
