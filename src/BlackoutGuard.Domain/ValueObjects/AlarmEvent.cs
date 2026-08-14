namespace BlackoutGuard.Domain.ValueObjects;

public sealed record AlarmEvent(
    string Code,
    string Severity,
    string Message,
    DateTime TimestampUtc
);
