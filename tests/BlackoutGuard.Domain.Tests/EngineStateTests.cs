using System.Reflection;
using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Domain.ValueObjects;

namespace BlackoutGuard.Domain.Tests.ValueObjects;

public class EngineStateTests
{
    [Fact]
    public void Empty_CreatesCorrectState_WithGivenFacilityId()
    {
        var facilityId = Guid.NewGuid();

        var state = EngineState.Empty(facilityId);

        Assert.NotNull(state.Loads);
        Assert.Empty(state.Loads);
        Assert.NotNull(state.Rules);
        Assert.Empty(state.Rules);
        Assert.NotNull(state.CooldownStates);
        Assert.Empty(state.CooldownStates);
        Assert.Equal(facilityId, state.FacilityId);
        Assert.Equal(0, state.Version);
    }

    [Fact]
    public void IsValid_ReturnsTrue_ForValidState()
    {
        var state = EngineState.Empty(Guid.NewGuid());

        var result = InvokeIsValid(state);

        Assert.True(result);
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenLoadsIsNull()
    {
        var state = EngineState.Empty(Guid.NewGuid());

        SetProperty(state, "Loads", null);

        var result = InvokeIsValid(state);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenRulesIsNull()
    {
        var state = EngineState.Empty(Guid.NewGuid());

        SetProperty(state, "Rules", null);

        var result = InvokeIsValid(state);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenCooldownStatesIsNull()
    {
        var state = EngineState.Empty(Guid.NewGuid());

        SetProperty(state, "CooldownStates", null);

        var result = InvokeIsValid(state);

        Assert.False(result);
    }

    [Fact]
    public void LoadCooldownInfo_IsImmutableRecord_WithNullableTimestamps()
    {
        var info = new LoadCooldownInfo(null, null, null);

        Assert.Null(info.LastShedAt);
        Assert.Null(info.LastRestoredAt);
        Assert.Null(info.CooldownUntil);
    }

    private static bool InvokeIsValid(EngineState state)
    {
        var method = typeof(EngineState).GetMethod(
            "IsValid",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (bool)method.Invoke(state, null)!;
    }

    private static void SetProperty(EngineState state, string propertyName, object? value)
    {
        var property = typeof(EngineState).GetProperty(propertyName);
        Assert.NotNull(property);
        property.SetValue(state, value);
    }
}
