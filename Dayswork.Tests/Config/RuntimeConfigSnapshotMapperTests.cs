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
            HardCapTime = 999,
            StuckInitialWaitMinutes = 0,
            StuckPostTeleportWaitMinutes = -2,
            WorkerWalkPixelsPerTick = 0,
            WorkerActionAnimationMs = 0,
            WorkerEntranceHoldTicks = -4,
            EnergyTierEnergy = new Dictionary<string, int> { ["FullDay"] = 0 },
            EnergyTierPrice = new Dictionary<string, int> { ["FullDay"] = -5 },
            WorkActionCosts = new Dictionary<string, int> { ["AxeSwing"] = -1 },
        });

        var defaults = ModConfig.CreateDefaults();
        Assert.Equal(1000, normalized.HardCapTime);
        Assert.Equal(1, normalized.StuckInitialWaitMinutes);
        Assert.Equal(1, normalized.StuckPostTeleportWaitMinutes);
        Assert.Equal(defaults.WorkerWalkPixelsPerTick, normalized.WorkerWalkPixelsPerTick);
        Assert.Equal(1, normalized.WorkerActionAnimationMs);
        Assert.Equal(0, normalized.WorkerEntranceHoldTicks);
        Assert.Equal(defaults.WorkOnHolidays, normalized.WorkOnHolidays);
        Assert.Equal("Either", normalized.PreferredCropStore);
        Assert.Equal(defaults.EnergyTierEnergy["FullDay"], normalized.EnergyTierEnergy["FullDay"]);
        Assert.Equal(defaults.EnergyTierPrice["FullDay"], normalized.EnergyTierPrice["FullDay"]);
        Assert.Equal(defaults.WorkActionCosts["AxeSwing"], normalized.WorkActionCosts["AxeSwing"]);
    }

    [Fact]
    public void BuildSnapshot_applies_edited_values()
    {
        var config = ModConfig.CreateDefaults();
        config.WorkOnHolidays = false;
        config.EnergyTierEnergy["FullDay"] = 222;
        config.EnergyTierPrice["FullDay"] = 999;
        config.WorkActionCosts["AxeSwing"] = 7;

        var snapshot = RuntimeConfigSnapshotMapper.BuildSnapshot(config);

        Assert.False(snapshot.WorkOnHolidays);
        Assert.Equal(222, snapshot.EnergyTierEnergy[EnergyTier.FullDay]);
        Assert.Equal(999, snapshot.EnergyTierPrice[EnergyTier.FullDay]);
        Assert.Equal(7, snapshot.WorkActionCosts[WorkActionKind.AxeSwing]);
    }

    [Theory]
    [InlineData("Pierre", "Pierre")]
    [InlineData("Joja", "Joja")]
    [InlineData("Bogus", "Either")]
    public void Normalize_round_trips_preferred_crop_store(string configured, string expected)
    {
        var normalized = RuntimeConfigSnapshotMapper.Normalize(new ModConfig { PreferredCropStore = configured });

        Assert.Equal(expected, normalized.PreferredCropStore);
    }

    [Fact]
    public void Normalize_logs_per_key_fallbacks()
    {
        var warnings = new List<string>();
        RuntimeConfigSnapshotMapper.Normalize(new ModConfig
        {
            EnergyTierEnergy = new Dictionary<string, int>(),
            EnergyTierPrice = new Dictionary<string, int>(),
            WorkActionCosts = new Dictionary<string, int>(),
        }, warnings.Add);

        Assert.Contains(warnings, w => w.StartsWith("ConfigFallback_EnergyTierEnergy_"));
        Assert.Contains(warnings, w => w.StartsWith("ConfigFallback_EnergyTierPrice_"));
        Assert.Contains(warnings, w => w.StartsWith("ConfigFallback_WorkActionCosts_"));
    }
}
