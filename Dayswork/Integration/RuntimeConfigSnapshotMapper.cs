using Dayswork.Core.Config;
using Dayswork.Core.Domain;

namespace Dayswork.Integration;

internal static class RuntimeConfigSnapshotMapper
{
    public static ModConfig Normalize(ModConfig config)
    {
        var defaults = ModConfig.CreateDefaults();

        return new ModConfig
        {
            BaseRate = Math.Max(0, config.BaseRate),
            WaterCropsRate = Math.Max(0, config.WaterCropsRate),
            HarvestCropsRate = Math.Max(0, config.HarvestCropsRate),
            CollectFruitRate = Math.Max(0, config.CollectFruitRate),
            FeedAnimalsRate = Math.Max(0, config.FeedAnimalsRate),
            PetAnimalsRate = Math.Max(0, config.PetAnimalsRate),
            CollectAnimalProductsRate = Math.Max(0, config.CollectAnimalProductsRate),
            CutTreesRate = Math.Max(0, config.CutTreesRate),
            ClearRocksRate = Math.Max(0, config.ClearRocksRate),
            ClearWeedsRate = Math.Max(0, config.ClearWeedsRate),
            ClearGrassRate = Math.Max(0, config.ClearGrassRate),
            AverageSpeedConstant = config.AverageSpeedConstant > 0
                ? config.AverageSpeedConstant
                : defaults.AverageSpeedConstant,
            HardCapTime = Math.Clamp(config.HardCapTime, 1000, 2600),
            StuckInitialWaitMinutes = Math.Max(1, config.StuckInitialWaitMinutes),
            StuckPostTeleportWaitMinutes = Math.Max(1, config.StuckPostTeleportWaitMinutes),
        };
    }

    public static ConfigSnapshot BuildSnapshot(ModConfig config)
    {
        var normalized = Normalize(config);
        var increments = new Dictionary<TaskKind, int>
        {
            [TaskKind.WaterCrops] = normalized.WaterCropsRate,
            [TaskKind.HarvestCrops] = normalized.HarvestCropsRate,
            [TaskKind.CollectFruit] = normalized.CollectFruitRate,
            [TaskKind.FeedAnimals] = normalized.FeedAnimalsRate,
            [TaskKind.PetAnimals] = normalized.PetAnimalsRate,
            [TaskKind.CollectAnimalProducts] = normalized.CollectAnimalProductsRate,
            [TaskKind.CutTrees] = normalized.CutTreesRate,
            [TaskKind.ClearRocks] = normalized.ClearRocksRate,
            [TaskKind.ClearWeeds] = normalized.ClearWeedsRate,
            [TaskKind.ClearGrass] = normalized.ClearGrassRate,
        };

        return ConfigSnapshotFactory.Create(
            normalized.BaseRate,
            increments,
            normalized.AverageSpeedConstant,
            normalized.HardCapTime,
            normalized.StuckInitialWaitMinutes,
            normalized.StuckPostTeleportWaitMinutes);
    }
}
