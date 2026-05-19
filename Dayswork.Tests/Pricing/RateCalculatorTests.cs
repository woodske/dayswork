namespace Dayswork.Tests.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Pricing;
using Dayswork.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

public class RateCalculatorTests
{
    private readonly RateCalculator _calc = new();
    private readonly IConfigSnapshot _cfg  = ConfigDefaults.Build();

    // ── [Fact] ───────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyTasks_ReturnsBaseRate()
    {
        Assert.Equal(_cfg.BaseRate, _calc.Calculate(Array.Empty<TaskKind>(), _cfg, isRaining: false));
    }

    [Fact]
    public void EmptyTasks_ReturnsBaseRate_WhenRaining()
    {
        Assert.Equal(_cfg.BaseRate, _calc.Calculate(Array.Empty<TaskKind>(), _cfg, isRaining: true));
    }

    [Fact]
    public void SingleTask_ReturnsBaseRatePlusIncrement()
    {
        var rate = _calc.Calculate(new[] { TaskKind.HarvestCrops }, _cfg, isRaining: false);
        Assert.Equal(_cfg.BaseRate + _cfg.TaskIncrements[TaskKind.HarvestCrops], rate);
    }

    [Fact]
    public void WaterCrops_IncludedWhenNotRaining()
    {
        var rate = _calc.Calculate(new[] { TaskKind.WaterCrops }, _cfg, isRaining: false);
        Assert.Equal(_cfg.BaseRate + _cfg.TaskIncrements[TaskKind.WaterCrops], rate);
    }

    [Fact]
    public void WaterCrops_ExcludedWhenRaining()
    {
        var withRain    = _calc.Calculate(new[] { TaskKind.WaterCrops }, _cfg, isRaining: true);
        var withoutTask = _calc.Calculate(Array.Empty<TaskKind>(), _cfg, isRaining: false);
        Assert.Equal(withoutTask, withRain);
    }

    [Fact]
    public void OtherTasks_UnaffectedByRain()
    {
        var dry   = _calc.Calculate(new[] { TaskKind.HarvestCrops }, _cfg, isRaining: false);
        var rainy = _calc.Calculate(new[] { TaskKind.HarvestCrops }, _cfg, isRaining: true);
        Assert.Equal(dry, rainy);
    }

    [Fact]
    public void AllTasks_NotRaining_ReturnsSumOfAll()
    {
        var expected = _cfg.BaseRate + _cfg.TaskIncrements.Values.Sum();
        var actual   = _calc.Calculate(Enum.GetValues<TaskKind>(), _cfg, isRaining: false);
        Assert.Equal(expected, actual);
    }

    // ── [Property] — PBT-03 ─────────────────────────────────────────────────

    [Property(MaxTest = 1000)]
    public Property Rate_AlwaysAtLeastBaseRate() =>
        Prop.ForAll(
            ConfigSnapshotGen.Snapshot(),
            PricingGen.TaskSubset(),
            (config, tasks) =>
                _calc.Calculate(tasks, config, isRaining: false) >= config.BaseRate
             && _calc.Calculate(tasks, config, isRaining: true)  >= config.BaseRate);

    [Property(MaxTest = 1000)]
    public Property Rate_EqualsBasePlusSumWhenNotRaining() =>
        Prop.ForAll(
            ConfigSnapshotGen.Snapshot(),
            PricingGen.TaskSubset(),
            (config, tasks) =>
            {
                var expected = config.BaseRate + tasks.Sum(t => config.TaskIncrements[t]);
                return _calc.Calculate(tasks, config, isRaining: false) == expected;
            });

    [Property(MaxTest = 1000)]
    public Property Rain_EquivalentToRemovingWaterCrops() =>
        Prop.ForAll(
            ConfigSnapshotGen.Snapshot(),
            PricingGen.TaskSubset(),
            (config, tasks) =>
            {
                var withRain   = _calc.Calculate(tasks, config, isRaining: true);
                var withoutWet = _calc.Calculate(
                    tasks.Where(t => t != TaskKind.WaterCrops).ToList(),
                    config, isRaining: false);
                return withRain == withoutWet;
            });

    [Property(MaxTest = 1000)]
    public Property Rate_IndependentOfEnumerationOrder() =>
        Prop.ForAll(
            ConfigSnapshotGen.Snapshot(),
            PricingGen.TaskSubset(),
            (config, tasks) =>
            {
                var forward  = _calc.Calculate(tasks, config, isRaining: false);
                var reversed = _calc.Calculate(tasks.Reverse().ToList(), config, isRaining: false);
                return forward == reversed;
            });
}
