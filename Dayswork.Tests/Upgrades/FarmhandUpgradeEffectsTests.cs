namespace Dayswork.Tests.Upgrades;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Upgrades;
using Xunit;

public sealed class FarmhandUpgradeEffectsTests
{
    [Fact]
    public void Apply_NoUpgrades_ReturnsEquivalentConfig()
    {
        var config = ConfigDefaults.Build();

        var upgraded = FarmhandUpgradeEffects.Apply(config, FarmhandUpgradeState.Empty);

        Assert.Equal(config, upgraded);
    }

    [Fact]
    public void Apply_SpeedUpgrade_AddsOneToConfiguredWalkSpeed()
    {
        var config = ConfigDefaults.Build() with { WorkerWalkPixelsPerTick = 3.5f };
        var state = FarmhandUpgradeState.Empty.MarkPurchased(FarmhandUpgradeKind.Speed);

        var upgraded = FarmhandUpgradeEffects.Apply(config, state);

        Assert.Equal(4.5f, upgraded.WorkerWalkPixelsPerTick);
    }

    [Fact]
    public void Apply_BothSpeedTiers_AddsTwoToConfiguredWalkSpeed()
    {
        var config = ConfigDefaults.Build() with { WorkerWalkPixelsPerTick = 3.5f };
        var state = FarmhandUpgradeState.Empty
            .MarkPurchased(FarmhandUpgradeKind.Speed)
            .MarkPurchased(FarmhandUpgradeKind.Speed2);

        var upgraded = FarmhandUpgradeEffects.Apply(config, state);

        Assert.Equal(5.5f, upgraded.WorkerWalkPixelsPerTick);
    }

    [Fact]
    public void Apply_SecondSpeedTierOnly_AddsOneToConfiguredWalkSpeed()
    {
        var config = ConfigDefaults.Build() with { WorkerWalkPixelsPerTick = 3.5f };
        var state = FarmhandUpgradeState.Empty.MarkPurchased(FarmhandUpgradeKind.Speed2);

        var upgraded = FarmhandUpgradeEffects.Apply(config, state);

        Assert.Equal(4.5f, upgraded.WorkerWalkPixelsPerTick);
    }

    [Fact]
    public void Apply_EnergyUpgrade_AddsFiftyToEveryTier()
    {
        var config = ConfigDefaults.Build();
        var state = FarmhandUpgradeState.Empty.MarkPurchased(FarmhandUpgradeKind.Energy);

        var upgraded = FarmhandUpgradeEffects.Apply(config, state);

        Assert.Equal(config.EnergyTierEnergy[EnergyTier.HalfDay] + 50, upgraded.EnergyTierEnergy[EnergyTier.HalfDay]);
        Assert.Equal(config.EnergyTierEnergy[EnergyTier.FullDay] + 50, upgraded.EnergyTierEnergy[EnergyTier.FullDay]);
        Assert.Equal(config.EnergyTierEnergy[EnergyTier.Overtime] + 50, upgraded.EnergyTierEnergy[EnergyTier.Overtime]);
    }
}
