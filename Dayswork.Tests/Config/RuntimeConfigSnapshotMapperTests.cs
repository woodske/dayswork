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
    public void Normalize_clamps_invalid_redesign_values_to_a_valid_config()
    {
        var normalized = RuntimeConfigSnapshotMapper.Normalize(new ModConfig
        {
            HardCapTime = 999,
            StuckInitialWaitMinutes = 0,
            StuckPostTeleportWaitMinutes = -2,
            WorkerWalkPixelsPerTick = 0,
            WorkerActionAnimationMs = 0,
            WorkerEntranceHoldTicks = -4,
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
        Assert.Equal(1000, normalized.HardCapTime);
        Assert.Equal(1, normalized.StuckInitialWaitMinutes);
        Assert.Equal(1, normalized.StuckPostTeleportWaitMinutes);
        Assert.Equal(defaults.WorkerWalkPixelsPerTick, normalized.WorkerWalkPixelsPerTick);
        Assert.Equal(1, normalized.WorkerActionAnimationMs);
        Assert.Equal(0, normalized.WorkerEntranceHoldTicks);
        Assert.Equal(defaults.WorkerDailyEnergyCapacity, normalized.WorkerDailyEnergyCapacity);
        Assert.Equal(defaults.OutdoorBandThresholds["Small"], normalized.OutdoorBandThresholds["Small"]);
        Assert.Equal(defaults.WorkActionCosts["AxeSwing"], normalized.WorkActionCosts["AxeSwing"]);
    }

    [Fact]
    public void BuildSnapshot_applies_edited_values()
    {
        var config = ModConfig.CreateDefaults();
        config.OutdoorBandThresholds["Small"] = 90;
        config.WorkerDailyEnergyCapacity = 333;
        config.WorkActionCosts["AxeSwing"] = 7;

        var snapshot = RuntimeConfigSnapshotMapper.BuildSnapshot(config);

        Assert.Equal(90, snapshot.OutdoorBandThresholds[OutdoorBandSize.Small]);
        Assert.Equal(333, snapshot.WorkerDailyEnergyCapacity);
        Assert.Equal(7, snapshot.WorkActionCosts[WorkActionKind.AxeSwing]);
    }

    [Fact]
    public void Normalize_logs_per_key_fallbacks_and_repairs_outdoor_threshold_order()
    {
        var warnings = new List<string>();
        var normalized = RuntimeConfigSnapshotMapper.Normalize(new ModConfig
        {
            OutdoorBandThresholds = new Dictionary<string, int>
            {
                ["Small"] = 100,
                ["Medium"] = 20,
                ["Large"] = 10,
            },
            OutdoorServiceBandPrices = new Dictionary<string, int>(),
            AnimalBuildingPrices = new Dictionary<string, int>(),
            GreenhouseServicePrices = new Dictionary<string, int>(),
            WorkActionCosts = new Dictionary<string, int>(),
        }, warnings.Add);

        var defaults = ModConfig.CreateDefaults();
        Assert.Equal(defaults.OutdoorBandThresholds["Medium"], normalized.OutdoorBandThresholds["Medium"]);
        Assert.Equal(defaults.OutdoorBandThresholds["Large"], normalized.OutdoorBandThresholds["Large"]);
        Assert.Contains("ConfigFallback_OutdoorBandThresholds_Medium", warnings);
        Assert.Contains("ConfigFallback_OutdoorBandThresholds_Large", warnings);
        Assert.Contains(
            $"ConfigFallback_{nameof(ModConfig.OutdoorServiceBandPrices)}_{new string("WaterCrops|Small".Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray())}_Default150",
            warnings);
    }
}
