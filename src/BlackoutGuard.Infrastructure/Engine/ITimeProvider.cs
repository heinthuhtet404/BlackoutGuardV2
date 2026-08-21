using System;

namespace BlackoutGuard.Infrastructure.Engine;

public interface ITimeProvider
{
    DateTime UtcNow { get; }
}

public sealed class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}