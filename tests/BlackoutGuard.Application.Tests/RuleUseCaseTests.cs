using System.Data;
using BlackoutGuard.Application;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Application.UseCases.Rules;

namespace BlackoutGuard.Application.Tests.UseCases.Rules;

public class RuleUseCaseTests
{
    private static (Fakes Fakes, RuleDto Rule) CreateRule(
        string parameterKey = "FREQ_LOW",
        double minValue = 47.0,
        double maxValue = 49.5)
    {
        var facilityId = Guid.NewGuid();
        var fakes = new Fakes();
        var rule = new RuleDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            Name = "Under-frequency Trip",
            ParameterKey = parameterKey,
            MinValue = minValue,
            MaxValue = maxValue,
            CooldownSeconds = 30,
            Unit = "Hz",
            IsActive = true
        };
        fakes.RuleRepo.Rules.Add(rule);
        return (fakes, rule);
    }

    [Fact]
    public async Task Update_ShouldSucceed_WithinBounds()
    {
        var (fakes, rule) = CreateRule();

        var useCase = fakes.BuildUpdateUseCase();
        var result = await useCase.ExecuteAsync(new UpdateRuleRequest
        {
            RuleId = rule.Id,
            FacilityId = rule.FacilityId,
            MinValue = 46.0,
            MaxValue = 49.0,
            Name = "Updated Frequency Rule"
        });

        Assert.True(result.IsSuccess);
        Assert.True(fakes.TxCommitted);
        var updated = fakes.RuleRepo.Rules.Single();
        Assert.Equal(46.0, updated.MinValue);
        Assert.Equal(49.0, updated.MaxValue);
        Assert.Equal("Updated Frequency Rule", updated.Name);
    }

    [Fact]
    public async Task Update_ShouldReject_MinValueBelow45_ForFreqLow()
    {
        var (fakes, rule) = CreateRule("FREQ_LOW", 47.0, 49.5);

        var useCase = fakes.BuildUpdateUseCase();
        var result = await useCase.ExecuteAsync(new UpdateRuleRequest
        {
            RuleId = rule.Id,
            FacilityId = rule.FacilityId,
            MinValue = 44.9
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("45", result.ErrorMessage);
        Assert.False(fakes.TxCommitted);
    }

    [Fact]
    public async Task Update_ShouldReject_MaxValueAbove55_ForFreqHigh()
    {
        var (fakes, rule) = CreateRule("FREQ_HIGH", 50.0, 52.0);

        var useCase = fakes.BuildUpdateUseCase();
        var result = await useCase.ExecuteAsync(new UpdateRuleRequest
        {
            RuleId = rule.Id,
            FacilityId = rule.FacilityId,
            MaxValue = 55.1
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("55", result.ErrorMessage);
        Assert.False(fakes.TxCommitted);
    }

    [Fact]
    public async Task Update_ShouldReject_MinGreaterThanMax()
    {
        var (fakes, rule) = CreateRule("FREQ_LOW", 47.0, 49.5);

        var useCase = fakes.BuildUpdateUseCase();
        var result = await useCase.ExecuteAsync(new UpdateRuleRequest
        {
            RuleId = rule.Id,
            FacilityId = rule.FacilityId,
            MinValue = 50.0,
            MaxValue = 48.0
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("min_value", result.ErrorMessage);
        Assert.False(fakes.TxCommitted);
    }

    [Fact]
    public async Task Update_ShouldPass_ExactBoundaryValues()
    {
        var (fakes, rule) = CreateRule("FREQ_LOW", 47.0, 49.5);

        var useCase = fakes.BuildUpdateUseCase();
        var result = await useCase.ExecuteAsync(new UpdateRuleRequest
        {
            RuleId = rule.Id,
            FacilityId = rule.FacilityId,
            MinValue = 45.0,
            MaxValue = 55.0
        });

        Assert.True(result.IsSuccess);
        Assert.True(fakes.TxCommitted);
        var updated = fakes.RuleRepo.Rules.Single();
        Assert.Equal(45.0, updated.MinValue);
        Assert.Equal(55.0, updated.MaxValue);
    }

    [Fact]
    public async Task Update_ShouldReject_InvalidParameterKey()
    {
        var (fakes, rule) = CreateRule();

        var useCase = fakes.BuildUpdateUseCase();
        var result = await useCase.ExecuteAsync(new UpdateRuleRequest
        {
            RuleId = rule.Id,
            FacilityId = rule.FacilityId,
            ParameterKey = "TEMP_HIGH"
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("TEMP_HIGH", result.ErrorMessage);
        Assert.False(fakes.TxCommitted);
    }

    [Fact]
    public async Task Update_ShouldFail_WhenRuleNotFound()
    {
        var fakes = new Fakes();
        var useCase = fakes.BuildUpdateUseCase();

        var result = await useCase.ExecuteAsync(new UpdateRuleRequest
        {
            RuleId = Guid.NewGuid(),
            FacilityId = Guid.NewGuid(),
            MinValue = 46.0
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Update_ShouldFail_WhenRuleBelongsToDifferentFacility()
    {
        var (fakes, rule) = CreateRule();

        var useCase = fakes.BuildUpdateUseCase();
        var result = await useCase.ExecuteAsync(new UpdateRuleRequest
        {
            RuleId = rule.Id,
            FacilityId = Guid.NewGuid(),
            MinValue = 46.0
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Update_ShouldAllowNonFrequencyRules_OutsideFreqBounds()
    {
        var (fakes, rule) = CreateRule("VOLT_LOW", 200.0, 210.0);

        var useCase = fakes.BuildUpdateUseCase();
        var result = await useCase.ExecuteAsync(new UpdateRuleRequest
        {
            RuleId = rule.Id,
            FacilityId = rule.FacilityId,
            MinValue = 190.0,
            MaxValue = 230.0
        });

        Assert.True(result.IsSuccess);
        Assert.True(fakes.TxCommitted);
    }

    [Fact]
    public async Task List_ShouldReturnOnlyFacilityRules()
    {
        var fakes = new Fakes();
        var facilityA = Guid.NewGuid();
        var facilityB = Guid.NewGuid();

        fakes.RuleRepo.Rules.Add(new RuleDto
        {
            Id = Guid.NewGuid(), FacilityId = facilityA, Name = "A1",
            ParameterKey = "FREQ_LOW", MinValue = 47, MaxValue = 49.5
        });
        fakes.RuleRepo.Rules.Add(new RuleDto
        {
            Id = Guid.NewGuid(), FacilityId = facilityA, Name = "A2",
            ParameterKey = "FREQ_HIGH", MinValue = 50.5, MaxValue = 52
        });
        fakes.RuleRepo.Rules.Add(new RuleDto
        {
            Id = Guid.NewGuid(), FacilityId = facilityB, Name = "B1",
            ParameterKey = "VOLT_LOW", MinValue = 200, MaxValue = 210
        });

        var useCase = new ListRulesUseCase(fakes.RuleRepo);
        var result = await useCase.ExecuteAsync(facilityA);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.All(result.Value, r => Assert.Equal(facilityA, r.FacilityId));
        Assert.DoesNotContain(result.Value, r => r.Name == "B1");
    }

    private sealed class Fakes
    {
        public FakeRuleRepository RuleRepo { get; } = new();
        public FakeTxFactory TxFactory { get; } = new();

        public bool TxCommitted => TxFactory.CurrentTx?.Committed ?? false;

        public UpdateRuleUseCase BuildUpdateUseCase()
        {
            var executionStrategy = new FakeExecutionStrategy();
            return new UpdateRuleUseCase(RuleRepo, TxFactory, executionStrategy);
        }
    }

    private sealed class FakeExecutionStrategy : IExecutionStrategy
    {
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct = default)
        {
            return await operation();
        }
    }

    private sealed class FakeTxFactory : IDbTransactionFactory
    {
        public FakeTransaction? CurrentTx { get; private set; }

        public Task<IDataTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken ct = default)
        {
            CurrentTx = new FakeTransaction();
            return Task.FromResult<IDataTransaction>(CurrentTx);
        }
    }

    private sealed class FakeTransaction : IDataTransaction
    {
        public bool Committed { get; private set; }

        public Task CommitAsync(CancellationToken ct = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeRuleRepository : IRuleRepository
    {
        public List<RuleDto> Rules { get; } = new();

        public Task<List<RuleDto>> GetAllByFacilityAsync(Guid facilityId, CancellationToken ct = default)
        {
            var rules = Rules.Where(r => r.FacilityId == facilityId).ToList();
            return Task.FromResult(rules);
        }

        public Task<RuleDto?> GetByIdAsync(Guid ruleId, Guid facilityId, CancellationToken ct = default)
        {
            var rule = Rules.FirstOrDefault(r => r.Id == ruleId && r.FacilityId == facilityId);
            return Task.FromResult(rule);
        }

        public Task UpdateAsync(RuleDto rule, CancellationToken ct = default)
        {
            var index = Rules.FindIndex(r => r.Id == rule.Id && r.FacilityId == rule.FacilityId);
            if (index >= 0)
                Rules[index] = rule;
            return Task.CompletedTask;
        }
    }
}
