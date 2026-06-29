namespace Dayswork.Core.Upgrades;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public static class FarmhandUpgradeEffects
{
    public static ConfigSnapshot Apply(ConfigSnapshot config, FarmhandUpgradeState state)
    {
        if (state == FarmhandUpgradeState.Empty)
            return config;

        var speedBonus =
            (state.SpeedPurchased ? FarmhandUpgradeCatalog.Speed.SpeedBonus : 0)
            + (state.Speed2Purchased ? FarmhandUpgradeCatalog.Speed2.SpeedBonus : 0);
        var energyBonus = state.EnergyPurchased ? FarmhandUpgradeCatalog.Energy.EnergyBonus : 0;

        var tierEnergy = config.EnergyTierEnergy.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value + energyBonus);

        return ConfigSnapshotFactory.Create(
            config.HardCapTime,
            config.StuckInitialWaitMinutes,
            config.StuckPostTeleportWaitMinutes,
            config.WorkerWalkPixelsPerTick + speedBonus,
            config.WorkerActionAnimationMs,
            config.WorkerEntranceHoldTicks,
            config.WorkOnHolidays,
            config.EagerChestDeposits,
            tierEnergy,
            config.EnergyTierPrice,
            config.WorkActionCosts);
    }

    public static ContractTermsSnapshot AddEnergyBonus(ContractTermsSnapshot terms) =>
        new(
            terms.Pricing,
            terms.Energy with
            {
                DailyCapacity = terms.Energy.DailyCapacity + FarmhandUpgradeCatalog.Energy.EnergyBonus,
            });
}
