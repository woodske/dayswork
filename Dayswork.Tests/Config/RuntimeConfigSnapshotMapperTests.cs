namespace Dayswork.Tests.Config;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Integration;
using Xunit;

public class RuntimeConfigSnapshotMapperTests
{
    [Fact]
    public void BuildSnapshot_defaults_match_ConfigDefaults()
    {
        var expected = ConfigDefaults.Build();
        var actual = RuntimeConfigSnapshotMapper.BuildSnapshot(ModConfig.CreateDefaults());

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Normalize_clamps_invalid_values_to_a_valid_config()
    {
        var normalized = RuntimeConfigSnapshotMapper.Normalize(new ModConfig
        {
            BaseRate = -5,
            WaterCropsRate = -1,
            HarvestCropsRate = -2,
            CollectFruitRate = -3,
            FeedAnimalsRate = -4,
            PetAnimalsRate = -5,
            CollectAnimalProductsRate = -6,
            CutTreesRate = -7,
            ClearRocksRate = -8,
            ClearWeedsRate = -9,
            ClearGrassRate = -10,
            AverageSpeedConstant = 0,
            HardCapTime = 999,
            StuckInitialWaitMinutes = 0,
            StuckPostTeleportWaitMinutes = -2,
            WorkerDailyEnergyCapacity = 0,
            OutdoorBandThresholds = new Dictionary<string, int>
            {
                ["Small"] = -1,
            },
            WorkActionCosts = new Dictionary<string, int>
            {
                ["AxeSwing"] = -1,
            },
        });

        var defaults = ModConfig.CreateDefaults();
        Assert.Equal(0, normalized.BaseRate);
        Assert.Equal(0, normalized.WaterCropsRate);
        Assert.Equal(0, normalized.ClearGrassRate);
        Assert.Equal(defaults.AverageSpeedConstant, normalized.AverageSpeedConstant);
        Assert.Equal(1000, normalized.HardCapTime);
        Assert.Equal(1, normalized.StuckInitialWaitMinutes);
        Assert.Equal(1, normalized.StuckPostTeleportWaitMinutes);
        Assert.Equal(defaults.WorkerDailyEnergyCapacity, normalized.WorkerDailyEnergyCapacity);
        Assert.Equal(defaults.OutdoorBandThresholds["Small"], normalized.OutdoorBandThresholds["Small"]);
        Assert.Equal(defaults.WorkActionCosts["AxeSwing"], normalized.WorkActionCosts["AxeSwing"]);
    }

    [Fact]
    public void BuildSnapshot_uses_normalized_values_for_rates_and_thresholds()
    {
        var snapshot = RuntimeConfigSnapshotMapper.BuildSnapshot(new ModConfig
        {
            BaseRate = -5,
            WaterCropsRate = -1,
            HardCapTime = 4000,
            StuckInitialWaitMinutes = 0,
            StuckPostTeleportWaitMinutes = 0,
            AverageSpeedConstant = -1,
            WorkerDailyEnergyCapacity = 0,
        });

        var defaults = ModConfig.CreateDefaults();
        Assert.Equal(0, snapshot.BaseRate);
        Assert.Equal(0, snapshot.TaskIncrements[TaskKind.WaterCrops]);
        Assert.Equal(2600, snapshot.HardCapTime);
        Assert.Equal(1, snapshot.StuckInitialWaitMinutes);
        Assert.Equal(1, snapshot.StuckPostTeleportWaitMinutes);
        Assert.Equal(defaults.AverageSpeedConstant, snapshot.AverageSpeedConstant);
        Assert.Equal(defaults.WorkerDailyEnergyCapacity, snapshot.WorkerDailyEnergyCapacity);
        Assert.Equal(defaults.WorkActionCosts["AxeSwing"], snapshot.WorkActionCosts[WorkActionKind.AxeSwing]);
    }
}
