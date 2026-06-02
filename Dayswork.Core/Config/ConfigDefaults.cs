namespace Dayswork.Core.Config;

using System.Collections.ObjectModel;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

public static class ConfigDefaults
{
    public static IConfigSnapshot Build()
    {
        // Purchased energy tiers: Energy becomes the worker's daily capacity; Price is paid up front
        // (one-time) or each eligible morning (recurring). Overtime is priced at a premium per energy.
        // Starting values — tuned via playtest.
        var energyTierEnergy = new Dictionary<EnergyTier, int>
        {
            [EnergyTier.HalfDay] = 100,
            [EnergyTier.FullDay] = 200,
            [EnergyTier.Overtime] = 300,
        };

        var energyTierPrice = new Dictionary<EnergyTier, int>
        {
            [EnergyTier.HalfDay] = 250,
            [EnergyTier.FullDay] = 450,
            [EnergyTier.Overtime] = 750,
        };

        var workActionCosts = new Dictionary<WorkActionKind, int>
        {
            [WorkActionKind.WaterTile] = 2,
            [WorkActionKind.HarvestCrop] = 1,
            [WorkActionKind.HarvestFruit] = 1,
            [WorkActionKind.FeedAnimal] = 1,
            [WorkActionKind.PetAnimal] = 1,
            [WorkActionKind.CollectAnimalProduct] = 1,
            [WorkActionKind.AxeSwing] = 2,
            [WorkActionKind.PickaxeSwing] = 2,
            [WorkActionKind.ScytheSwing] = 1,
        };

        return ConfigSnapshotFactory.Create(
            hardCapTime: 2000,
            stuckInitialWaitMinutes: 10,
            stuckPostTeleportWaitMinutes: 10,
            workerWalkPixelsPerTick: 2f,
            workerActionAnimationMs: 650,
            workerEntranceHoldTicks: 120,
            energyTierEnergy: new ReadOnlyDictionary<EnergyTier, int>(energyTierEnergy),
            energyTierPrice: new ReadOnlyDictionary<EnergyTier, int>(energyTierPrice),
            workActionCosts: new ReadOnlyDictionary<WorkActionKind, int>(workActionCosts));
    }
}
