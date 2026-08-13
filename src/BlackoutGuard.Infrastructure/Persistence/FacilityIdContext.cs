using System.Runtime.CompilerServices;

namespace BlackoutGuard.Infrastructure.Persistence;

public static class FacilityIdContext
{
    private static readonly AsyncLocal<Guid?> Current = new();

    public static Guid? FacilityId
    {
        get => Current.Value;
        set => Current.Value = value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Guid? GetCurrent() => Current.Value;
}
