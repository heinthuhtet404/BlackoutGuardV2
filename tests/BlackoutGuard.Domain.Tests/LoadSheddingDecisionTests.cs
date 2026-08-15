using BlackoutGuard.Domain.ValueObjects;

namespace BlackoutGuard.Domain.Tests.ValueObjects;

public class LoadSheddingDecisionTests
{
    [Fact]
    public void None_IsNone_IsTrue()
    {
        Assert.True(LoadSheddingDecision.None.IsNone);
        Assert.Empty(LoadSheddingDecision.None.RelayDecisions);
    }

    [Fact]
    public void Create_WithDecisions_IsNone_IsFalse()
    {
        var decisions = new[]
        {
            new RelayDecision(1, false, "frequency low"),
            new RelayDecision(2, false, "frequency low")
        };

        var result = LoadSheddingDecision.Create(decisions);

        Assert.False(result.IsNone);
        Assert.Equal(2, result.RelayDecisions.Count);
        Assert.Equal(1, result.RelayDecisions[0].RelayAddress);
        Assert.False(result.RelayDecisions[0].Energize);
        Assert.Equal("frequency low", result.RelayDecisions[0].Reason);
    }

    [Fact]
    public void Create_WithEmptyCollection_IsNone_IsTrue()
    {
        var result = LoadSheddingDecision.Create(Array.Empty<RelayDecision>());

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Create_ProducesIndependentSnapshot()
    {
        var decisions = new List<RelayDecision>
        {
            new(1, false, "reason")
        };

        var result = LoadSheddingDecision.Create(decisions);
        decisions.Add(new RelayDecision(2, true, "extra"));

        Assert.Single(result.RelayDecisions);
    }
}
