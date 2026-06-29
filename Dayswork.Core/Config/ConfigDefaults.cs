namespace Dayswork.Core.Config;

using System.Collections.ObjectModel;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

public static class ConfigDefaults
{
    public static ConfigSnapshot Build()
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
            [EnergyTier.FullDay] = 500,
            [EnergyTier.Overtime] = 1000,
        };

        var workActionCosts = new Dictionary<WorkActionKind, int>
        {
            [WorkActionKind.WaterTile] = 1,
            [WorkActionKind.HarvestCrop] = 1,
            [WorkActionKind.HarvestFruit] = 1,
            [WorkActionKind.FeedAnimal] = 1,
            [WorkActionKind.PetAnimal] = 1,
            [WorkActionKind.CollectAnimalProduct] = 1,
            [WorkActionKind.AxeSwing] = 2,
            [WorkActionKind.PickaxeSwing] = 2,
            [WorkActionKind.ScytheSwing] = 1,
            [WorkActionKind.HoeSwing] = 2,
            [WorkActionKind.PlantSeed] = 1,
            [WorkActionKind.ApplyFertilizer] = 1,
            // Machine work is priced per interaction, not per item: one charge to collect a
            // machine's ready output, one charge to (re)load an empty machine — regardless of how
            // many items the recipe consumes (a 5-fruit dehydrator load is one LoadMachine charge).
            [WorkActionKind.CollectMachine] = 1,
            [WorkActionKind.LoadMachine] = 1,
            // Fish ponds are collect-only (the player stocks the fish); one charge to collect a
            // pond's ready output, mirroring the machine collect charge.
            [WorkActionKind.CollectFishPond] = 1,
        };

        return ConfigSnapshotFactory.Create(
            hardCapTime: 2000,
            stuckInitialWaitMinutes: 10,
            stuckPostTeleportWaitMinutes: 10,
            workerWalkPixelsPerTick: 2f,
            workerActionAnimationMs: 650,
            workerEntranceHoldTicks: 120,
            workOnHolidays: true,
            eagerChestDeposits: true,
            energyTierEnergy: new ReadOnlyDictionary<EnergyTier, int>(energyTierEnergy),
            energyTierPrice: new ReadOnlyDictionary<EnergyTier, int>(energyTierPrice),
            workActionCosts: new ReadOnlyDictionary<WorkActionKind, int>(workActionCosts));
    }
}
