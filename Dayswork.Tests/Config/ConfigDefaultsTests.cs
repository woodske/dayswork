namespace Dayswork.Tests.Config;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Xunit;

public class ConfigDefaultsTests
{
    [Fact]
    public void Build_returns_non_null_snapshot()
    {
        IConfigSnapshot snapshot = ConfigDefaults.Build();
        Assert.NotNull(snapshot);
    }

    [Fact]
    public void Build_BaseRate_is_50()
    {
        Assert.Equal(50, ConfigDefaults.Build().BaseRate);
    }

    [Theory]
    [InlineData(TaskKind.WaterCrops,            20)]
    [InlineData(TaskKind.HarvestCrops,          25)]
    [InlineData(TaskKind.CollectFruit,          15)]
    [InlineData(TaskKind.FeedAnimals,           20)]
    [InlineData(TaskKind.PetAnimals,            10)]
    [InlineData(TaskKind.CollectAnimalProducts, 15)]
    [InlineData(TaskKind.CutTrees,              30)]
    [InlineData(TaskKind.ClearRocks,            20)]
    [InlineData(TaskKind.ClearWeeds,            20)]
    [InlineData(TaskKind.ClearGrass,            20)]
    public void Build_TaskIncrements_match_spec_rate_table(TaskKind kind, int expected)
    {
        Assert.Equal(expected, ConfigDefaults.Build().TaskIncrements[kind]);
    }

    [Fact]
    public void Build_TaskIncrements_covers_every_TaskKind_value()
    {
        var snapshot = ConfigDefaults.Build();
        var allKinds = Enum.GetValues<TaskKind>();
        Assert.Equal(allKinds.Length, snapshot.TaskIncrements.Count);
        foreach (var kind in allKinds)
            Assert.True(snapshot.TaskIncrements.ContainsKey(kind));
    }

    [Fact]
    public void Build_AverageSpeedConstant_is_positive()
    {
        Assert.True(ConfigDefaults.Build().AverageSpeedConstant > 0);
    }

    [Fact]
    public void Build_HardCapTime_is_2000()
    {
        Assert.Equal(2000, ConfigDefaults.Build().HardCapTime);
    }

    [Fact]
    public void Build_StuckInitialWaitMinutes_at_least_1()
    {
        Assert.True(ConfigDefaults.Build().StuckInitialWaitMinutes >= 1);
    }

    [Fact]
    public void Build_StuckPostTeleportWaitMinutes_at_least_1()
    {
        Assert.True(ConfigDefaults.Build().StuckPostTeleportWaitMinutes >= 1);
    }

    [Fact]
    public void Build_OutdoorBandThresholds_cover_every_band()
    {
        var snapshot = ConfigDefaults.Build();
        var allBands = Enum.GetValues<OutdoorBandSize>();
        Assert.Equal(allBands.Length, snapshot.OutdoorBandThresholds.Count);
        foreach (var band in allBands)
            Assert.True(snapshot.OutdoorBandThresholds.ContainsKey(band));
    }

    [Fact]
    public void Build_OutdoorServiceBandPrices_cover_every_expected_key()
    {
        var snapshot = ConfigDefaults.Build();
        var expectedCount = TaskKindSets.OutdoorServices.Count * Enum.GetValues<OutdoorBandSize>().Length;
        Assert.Equal(expectedCount, snapshot.OutdoorServiceBandPrices.Count);
    }

    [Fact]
    public void Build_AnimalBuildingPrices_cover_every_expected_key()
    {
        var snapshot = ConfigDefaults.Build();
        var expectedCount = TaskKindSets.AnimalServices.Count * Enum.GetValues<AnimalBuildingTier>().Length;
        Assert.Equal(expectedCount, snapshot.AnimalBuildingPrices.Count);
    }

    [Fact]
    public void Build_WorkActionCosts_cover_every_work_action()
    {
        var snapshot = ConfigDefaults.Build();
        var allActions = Enum.GetValues<WorkActionKind>();
        Assert.Equal(allActions.Length, snapshot.WorkActionCosts.Count);
        foreach (var action in allActions)
            Assert.True(snapshot.WorkActionCosts.ContainsKey(action));
    }

    [Fact]
    public void Build_WorkerDailyEnergyCapacity_is_positive()
    {
        Assert.True(ConfigDefaults.Build().WorkerDailyEnergyCapacity > 0);
    }

    [Fact]
    public void Build_is_deterministic()
    {
        Assert.Equal(ConfigDefaults.Build(), ConfigDefaults.Build());
    }
}
