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
        ConfigSnapshot snapshot = ConfigDefaults.Build();
        Assert.NotNull(snapshot);
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
    public void Build_EnergyTierEnergy_covers_every_tier_with_positive_values()
    {
        var snapshot = ConfigDefaults.Build();
        var allTiers = Enum.GetValues<EnergyTier>();
        Assert.Equal(allTiers.Length, snapshot.EnergyTierEnergy.Count);
        foreach (var tier in allTiers)
            Assert.True(snapshot.EnergyTierEnergy[tier] > 0);
    }

    [Fact]
    public void Build_EnergyTierPrice_covers_every_tier()
    {
        var snapshot = ConfigDefaults.Build();
        var allTiers = Enum.GetValues<EnergyTier>();
        Assert.Equal(allTiers.Length, snapshot.EnergyTierPrice.Count);
        foreach (var tier in allTiers)
            Assert.True(snapshot.EnergyTierPrice.ContainsKey(tier));
    }

    [Fact]
    public void Build_EnergyTiers_increase_with_capacity()
    {
        var snapshot = ConfigDefaults.Build();
        Assert.True(snapshot.EnergyTierEnergy[EnergyTier.HalfDay] < snapshot.EnergyTierEnergy[EnergyTier.FullDay]);
        Assert.True(snapshot.EnergyTierEnergy[EnergyTier.FullDay] < snapshot.EnergyTierEnergy[EnergyTier.Overtime]);
    }

    [Fact]
    public void Build_WorkOnHolidays_defaults_true()
    {
        Assert.True(ConfigDefaults.Build().WorkOnHolidays);
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
    public void Build_is_deterministic()
    {
        Assert.Equal(ConfigDefaults.Build(), ConfigDefaults.Build());
    }
}
