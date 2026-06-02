namespace Dayswork.Tests.Config;

using System.Collections.ObjectModel;
using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Xunit;

public sealed class ConfigValueResolverTests
{
    [Fact]
    public void ResolveEnergyTierEnergy_FallsBackToDefault_WhenKeyMissing()
    {
        var defaults = (ConfigSnapshot)ConfigDefaults.Build();
        var missing = defaults with
        {
            EnergyTierEnergy = new ReadOnlyDictionary<EnergyTier, int>(new Dictionary<EnergyTier, int>()),
        };

        var resolver = new ConfigValueResolver();
        var actual = resolver.ResolveEnergyTierEnergy(missing, EnergyTier.FullDay);

        Assert.True(actual.UsedDefault);
        Assert.Equal(defaults.EnergyTierEnergy[EnergyTier.FullDay], actual.Value);
    }

    [Fact]
    public void ResolveEnergyTierEnergy_UsesConfiguredValue_WhenPresent()
    {
        var defaults = (ConfigSnapshot)ConfigDefaults.Build();
        var custom = defaults with
        {
            EnergyTierEnergy = new ReadOnlyDictionary<EnergyTier, int>(new Dictionary<EnergyTier, int>
            {
                [EnergyTier.HalfDay] = 111,
                [EnergyTier.FullDay] = 222,
                [EnergyTier.Overtime] = 333,
            }),
        };

        var resolver = new ConfigValueResolver();
        var actual = resolver.ResolveEnergyTierEnergy(custom, EnergyTier.Overtime);

        Assert.False(actual.UsedDefault);
        Assert.Equal(333, actual.Value);
    }

    [Fact]
    public void ResolveEnergyTierPrice_FallsBackToDefault_WhenKeyMissing()
    {
        var defaults = (ConfigSnapshot)ConfigDefaults.Build();
        var missing = defaults with
        {
            EnergyTierPrice = new ReadOnlyDictionary<EnergyTier, int>(new Dictionary<EnergyTier, int>()),
        };

        var resolver = new ConfigValueResolver();
        var actual = resolver.ResolveEnergyTierPrice(missing, EnergyTier.HalfDay);

        Assert.True(actual.UsedDefault);
        Assert.Equal(defaults.EnergyTierPrice[EnergyTier.HalfDay], actual.Value);
    }

    [Fact]
    public void ResolveWorkActionCost_FallsBackToDefault_WhenKeyMissing()
    {
        var defaults = (ConfigSnapshot)ConfigDefaults.Build();
        var missingCosts = defaults with
        {
            WorkActionCosts = new ReadOnlyDictionary<WorkActionKind, int>(
                new Dictionary<WorkActionKind, int>()),
        };

        var resolver = new ConfigValueResolver();
        var actual = resolver.ResolveWorkActionCost(missingCosts, WorkActionKind.AxeSwing);

        Assert.True(actual.UsedDefault);
        Assert.Equal(defaults.WorkActionCosts[WorkActionKind.AxeSwing], actual.Value);
    }
}
