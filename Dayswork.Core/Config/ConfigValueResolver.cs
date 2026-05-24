namespace Dayswork.Core.Config;

using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

public sealed class ConfigValueResolver
{
    private static readonly IConfigSnapshot DefaultSnapshot = ConfigDefaults.Build();

    public ResolvedIntValue ResolveOutdoorBandThreshold(IConfigSnapshot config, OutdoorBandSize band)
    {
        if (config.OutdoorBandThresholds.TryGetValue(band, out var value) && value > 0)
            return new ResolvedIntValue(value, false);

        return new ResolvedIntValue(DefaultSnapshot.OutdoorBandThresholds[band], true);
    }

    public ResolvedIntValue ResolveOutdoorServiceBandPrice(IConfigSnapshot config, OutdoorPriceKey key)
    {
        if (config.OutdoorServiceBandPrices.TryGetValue(key, out var value) && value >= 0)
            return new ResolvedIntValue(value, false);

        return new ResolvedIntValue(DefaultSnapshot.OutdoorServiceBandPrices[key], true);
    }

    public ResolvedIntValue ResolveAnimalBuildingPrice(IConfigSnapshot config, AnimalBuildingPriceKey key)
    {
        if (config.AnimalBuildingPrices.TryGetValue(key, out var value) && value >= 0)
            return new ResolvedIntValue(value, false);

        return new ResolvedIntValue(DefaultSnapshot.AnimalBuildingPrices[key], true);
    }

    public ResolvedIntValue ResolveGreenhouseServicePrice(IConfigSnapshot config, GreenhousePriceKey key)
    {
        if (config.GreenhouseServicePrices.TryGetValue(key, out var value) && value >= 0)
            return new ResolvedIntValue(value, false);

        return new ResolvedIntValue(DefaultSnapshot.GreenhouseServicePrices[key], true);
    }

    public ResolvedIntValue ResolveWorkerDailyEnergyCapacity(IConfigSnapshot config)
    {
        if (config.WorkerDailyEnergyCapacity > 0)
            return new ResolvedIntValue(config.WorkerDailyEnergyCapacity, false);

        return new ResolvedIntValue(DefaultSnapshot.WorkerDailyEnergyCapacity, true);
    }

    public ResolvedIntValue ResolveWorkActionCost(IConfigSnapshot config, WorkActionKind action)
    {
        if (config.WorkActionCosts.TryGetValue(action, out var value) && value >= 0)
            return new ResolvedIntValue(value, false);

        return new ResolvedIntValue(DefaultSnapshot.WorkActionCosts[action], true);
    }
}
