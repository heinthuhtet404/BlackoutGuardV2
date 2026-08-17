namespace BlackoutGuard.Infrastructure.Engine;

public interface ISystemTimeProvider
{
    DateTime UtcNow { get; }
}

public sealed class UtcSystemTimeProvider : ISystemTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
