namespace Dayswork.Core.Config;

using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

public sealed class ConfigValueResolver
{
    private static readonly ConfigSnapshot DefaultSnapshot = ConfigDefaults.Build();

    public ResolvedIntValue ResolveEnergyTierEnergy(ConfigSnapshot config, EnergyTier tier)
    {
        if (config.EnergyTierEnergy.TryGetValue(tier, out var value) && value > 0)
            return new ResolvedIntValue(value, false);

        return new ResolvedIntValue(DefaultSnapshot.EnergyTierEnergy[tier], true);
    }

    public ResolvedIntValue ResolveEnergyTierPrice(ConfigSnapshot config, EnergyTier tier)
    {
        if (config.EnergyTierPrice.TryGetValue(tier, out var value) && value >= 0)
            return new ResolvedIntValue(value, false);

        return new ResolvedIntValue(DefaultSnapshot.EnergyTierPrice[tier], true);
    }

    public ResolvedIntValue ResolveWorkActionCost(ConfigSnapshot config, WorkActionKind action)
    {
        if (config.WorkActionCosts.TryGetValue(action, out var value) && value >= 0)
            return new ResolvedIntValue(value, false);

        return new ResolvedIntValue(DefaultSnapshot.WorkActionCosts[action], true);
    }
}
