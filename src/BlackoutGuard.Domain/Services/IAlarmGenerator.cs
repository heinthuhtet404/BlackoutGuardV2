using BlackoutGuard.Domain.Entities;

namespace BlackoutGuard.Domain.Services;

public interface IAlarmGenerator
{
    IEnumerable<Alarm> GenerateAlarms(GridState gridState);
}