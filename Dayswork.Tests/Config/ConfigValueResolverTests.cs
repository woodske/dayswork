namespace Dayswork.Tests.Config;

using System.Collections.ObjectModel;
using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Xunit;

public sealed class ConfigValueResolverTests
{
    [Fact]
    public void ResolveOutdoorServiceBandPrice_FallsBackToDefault_WhenKeyMissing()
    {
        var defaults = (ConfigSnapshot)ConfigDefaults.Build();
        var missingPrices = defaults with
        {
            OutdoorServiceBandPrices = new ReadOnlyDictionary<OutdoorPriceKey, int>(
                new Dictionary<OutdoorPriceKey, int>()),
        };

        var resolver = new ConfigValueResolver();
        var key = new OutdoorPriceKey(TaskKind.HarvestCrops, OutdoorBandSize.Medium);

        var actual = resolver.ResolveOutdoorServiceBandPrice(missingPrices, key);

        Assert.True(actual.UsedDefault);
        Assert.Equal(defaults.OutdoorServiceBandPrices[key], actual.Value);
    }

    [Fact]
    public void ResolveWorkerDailyEnergyCapacity_FallsBackToDefault_WhenInvalid()
    {
        var defaults = (ConfigSnapshot)ConfigDefaults.Build();
        var invalidCapacity = defaults with { WorkerDailyEnergyCapacity = 0 };

        var resolver = new ConfigValueResolver();
        var actual = resolver.ResolveWorkerDailyEnergyCapacity(invalidCapacity);

        Assert.True(actual.UsedDefault);
        Assert.Equal(defaults.WorkerDailyEnergyCapacity, actual.Value);
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
