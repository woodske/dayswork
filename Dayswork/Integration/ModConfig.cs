using Dayswork.Core.Config;
using Dayswork.Core.Domain;

namespace Dayswork.Integration;

public sealed class ModConfig
{
    private static readonly IConfigSnapshot DefaultSnapshot = ConfigDefaults.Build();

    public int BaseRate { get; set; } = DefaultSnapshot.BaseRate;
    public int WaterCropsRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.WaterCrops];
    public int HarvestCropsRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.HarvestCrops];
    public int CollectFruitRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.CollectFruit];
    public int FeedAnimalsRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.FeedAnimals];
    public int PetAnimalsRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.PetAnimals];
    public int CollectAnimalProductsRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.CollectAnimalProducts];
    public int CutTreesRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.CutTrees];
    public int ClearRocksRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.ClearRocks];
    public int ClearWeedsRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.ClearWeeds];
    public int ClearGrassRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.ClearGrass];
    public double AverageSpeedConstant { get; set; } = DefaultSnapshot.AverageSpeedConstant;
    public int HardCapTime { get; set; } = DefaultSnapshot.HardCapTime;
    public int StuckInitialWaitMinutes { get; set; } = DefaultSnapshot.StuckInitialWaitMinutes;
    public int StuckPostTeleportWaitMinutes { get; set; } = DefaultSnapshot.StuckPostTeleportWaitMinutes;

    public static ModConfig CreateDefaults() => new();

    public int GetTaskRate(TaskKind kind) => kind switch
    {
        TaskKind.WaterCrops            => WaterCropsRate,
        TaskKind.HarvestCrops          => HarvestCropsRate,
        TaskKind.CollectFruit          => CollectFruitRate,
        TaskKind.FeedAnimals           => FeedAnimalsRate,
        TaskKind.PetAnimals            => PetAnimalsRate,
        TaskKind.CollectAnimalProducts => CollectAnimalProductsRate,
        TaskKind.CutTrees              => CutTreesRate,
        TaskKind.ClearRocks            => ClearRocksRate,
        TaskKind.ClearWeeds            => ClearWeedsRate,
        TaskKind.ClearGrass            => ClearGrassRate,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public void SetTaskRate(TaskKind kind, int value)
    {
        switch (kind)
        {
            case TaskKind.WaterCrops:
                WaterCropsRate = value;
                break;
            case TaskKind.HarvestCrops:
                HarvestCropsRate = value;
                break;
            case TaskKind.CollectFruit:
                CollectFruitRate = value;
                break;
            case TaskKind.FeedAnimals:
                FeedAnimalsRate = value;
                break;
            case TaskKind.PetAnimals:
                PetAnimalsRate = value;
                break;
            case TaskKind.CollectAnimalProducts:
                CollectAnimalProductsRate = value;
                break;
            case TaskKind.CutTrees:
                CutTreesRate = value;
                break;
            case TaskKind.ClearRocks:
                ClearRocksRate = value;
                break;
            case TaskKind.ClearWeeds:
                ClearWeedsRate = value;
                break;
            case TaskKind.ClearGrass:
                ClearGrassRate = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }
}
